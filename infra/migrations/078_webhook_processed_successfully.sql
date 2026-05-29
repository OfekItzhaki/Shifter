-- Add processed_successfully column to webhook_event_logs
-- Allows retrying failed webhooks (previously, any logged event was considered "processed")
ALTER TABLE webhook_event_logs
ADD COLUMN IF NOT EXISTS processed_successfully BOOLEAN NOT NULL DEFAULT FALSE;

-- Backfill: mark all existing events as successfully processed
-- (they were logged before this fix, so we assume they succeeded)
UPDATE webhook_event_logs SET processed_successfully = TRUE WHERE processed_successfully = FALSE;
