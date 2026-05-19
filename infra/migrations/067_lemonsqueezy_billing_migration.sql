-- Migration 067: Migrate billing from Stripe to LemonSqueezy
-- Renames Stripe columns to LemonSqueezy equivalents and creates webhook_event_logs table.

-- Rename Stripe columns to LemonSqueezy on group_subscriptions
ALTER TABLE group_subscriptions
    RENAME COLUMN stripe_subscription_id TO lemonsqueezy_subscription_id;

ALTER TABLE group_subscriptions
    RENAME COLUMN stripe_customer_id TO lemonsqueezy_customer_id;

-- Drop old Stripe index and create new one
DROP INDEX IF EXISTS idx_group_subscriptions_stripe;
CREATE INDEX idx_group_subscriptions_lemonsqueezy ON group_subscriptions(lemonsqueezy_subscription_id);

-- Create webhook event log table for idempotent webhook processing
CREATE TABLE IF NOT EXISTS webhook_event_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_id VARCHAR(255) NOT NULL,
    event_type VARCHAR(100) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_webhook_event_logs_event_id ON webhook_event_logs(event_id);
CREATE INDEX idx_webhook_event_logs_processed_at ON webhook_event_logs(processed_at);

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('067') ON CONFLICT DO NOTHING;
