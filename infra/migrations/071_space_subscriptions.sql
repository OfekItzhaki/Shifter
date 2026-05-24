-- Migration 071: Space-level billing
-- Creates space_subscriptions table for per-space billing (replaces per-group model).
-- Adds 'migrated' as a valid status for group_subscriptions (no schema change needed since status is TEXT).

CREATE TABLE IF NOT EXISTS space_subscriptions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id UUID NOT NULL REFERENCES spaces(id),
    tier_id TEXT NOT NULL DEFAULT 'trial',
    status TEXT NOT NULL DEFAULT 'trialing',
    lemonsqueezy_subscription_id TEXT,
    lemonsqueezy_customer_id TEXT,
    trial_starts_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    trial_ends_at TIMESTAMPTZ NOT NULL DEFAULT (now() + INTERVAL '14 days'),
    current_period_start TIMESTAMPTZ,
    current_period_end TIMESTAMPTZ,
    peak_member_count INT NOT NULL DEFAULT 0,
    canceled_at TIMESTAMPTZ,
    auto_renew BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- One subscription per space
CREATE UNIQUE INDEX IF NOT EXISTS uq_space_subscriptions_space_id
    ON space_subscriptions(space_id);

-- For the expiry background job (queries by status)
CREATE INDEX IF NOT EXISTS idx_space_subscriptions_status
    ON space_subscriptions(status);

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('071') ON CONFLICT DO NOTHING;
