-- Subscription tiers and billing for groups
-- Peak member tracking prevents gaming (removing members before billing)

CREATE TABLE IF NOT EXISTS group_subscriptions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id UUID NOT NULL REFERENCES spaces(id),
    group_id UUID NOT NULL REFERENCES groups(id),
    tier_id TEXT NOT NULL DEFAULT 'trial', -- trial, starter, growth, team, org, unlimited
    status TEXT NOT NULL DEFAULT 'trialing', -- trialing, active, past_due, canceled
    stripe_subscription_id TEXT, -- Stripe subscription ID
    stripe_customer_id TEXT, -- Stripe customer ID
    trial_ends_at TIMESTAMPTZ,
    current_period_start TIMESTAMPTZ,
    current_period_end TIMESTAMPTZ,
    peak_member_count INT NOT NULL DEFAULT 0, -- highest member count in current period
    coupon_code TEXT, -- applied coupon
    discount_percent INT DEFAULT 0, -- 0-100
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    canceled_at TIMESTAMPTZ,
    UNIQUE(group_id)
);

CREATE INDEX idx_group_subscriptions_space ON group_subscriptions(space_id);
CREATE INDEX idx_group_subscriptions_stripe ON group_subscriptions(stripe_subscription_id);

-- Coupon codes
CREATE TABLE IF NOT EXISTS coupons (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code TEXT NOT NULL UNIQUE,
    discount_percent INT NOT NULL CHECK (discount_percent > 0 AND discount_percent <= 100),
    max_uses INT, -- NULL = unlimited
    current_uses INT NOT NULL DEFAULT 0,
    valid_from TIMESTAMPTZ NOT NULL DEFAULT now(),
    valid_until TIMESTAMPTZ, -- NULL = no expiry
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    description TEXT -- internal note
);

CREATE UNIQUE INDEX idx_coupons_code ON coupons(code);

-- Track peak member count history for auditing
CREATE TABLE IF NOT EXISTS peak_member_snapshots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    group_id UUID NOT NULL REFERENCES groups(id),
    period_start TIMESTAMPTZ NOT NULL,
    period_end TIMESTAMPTZ NOT NULL,
    peak_count INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_peak_snapshots_group ON peak_member_snapshots(group_id, period_start);
