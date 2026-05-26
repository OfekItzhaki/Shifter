-- Migration 075: Backfill space_subscriptions for spaces that predate the billing feature.
-- Inserts a trial subscription record for any space that doesn't already have one.
-- Uses the space's created_at as trial_starts_at and created_at + 14 days as trial_ends_at.
-- Status is set based on whether the trial period has elapsed.

INSERT INTO space_subscriptions (id, space_id, tier_id, status, trial_starts_at, trial_ends_at, auto_renew, peak_member_count, created_at, updated_at)
SELECT
    uuid_generate_v4(),
    s.id,
    'trial',
    CASE
        WHEN s.created_at + INTERVAL '14 days' < now() THEN 'expired'
        ELSE 'trialing'
    END,
    s.created_at,
    s.created_at + INTERVAL '14 days',
    true,
    0,
    now(),
    now()
FROM spaces s
WHERE NOT EXISTS (
    SELECT 1 FROM space_subscriptions ss WHERE ss.space_id = s.id
)
AND s.deleted_at IS NULL;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('075') ON CONFLICT DO NOTHING;
