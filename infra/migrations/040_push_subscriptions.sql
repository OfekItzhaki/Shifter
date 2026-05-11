-- Push subscriptions for Web Push notifications
CREATE TABLE push_subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    space_id UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    endpoint TEXT NOT NULL,
    p256dh TEXT NOT NULL,
    auth TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_push_sub_user_space_endpoint
        UNIQUE (user_id, space_id, endpoint)
);

CREATE INDEX ix_push_subscriptions_user_space
    ON push_subscriptions(user_id, space_id);

-- Enable RLS
ALTER TABLE push_subscriptions ENABLE ROW LEVEL SECURITY;

CREATE POLICY push_subscriptions_tenant_isolation ON push_subscriptions
    USING (space_id = current_setting('app.current_space_id')::uuid);
