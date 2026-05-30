-- Migration 081: Fix swap_requests schema to match EF entity model
-- The original migration 077 created columns with different names than what EF expects.

-- Add missing columns
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS group_id UUID;
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS initiator_person_id UUID;
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS target_person_id UUID;
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS initiator_shift_request_id UUID;
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS target_shift_request_id UUID;
ALTER TABLE swap_requests ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

-- Copy data from old columns to new columns (if any rows exist)
UPDATE swap_requests SET
    initiator_person_id = requester_person_id,
    initiator_shift_request_id = requester_slot_id,
    target_shift_request_id = target_slot_id
WHERE initiator_person_id IS NULL AND requester_person_id IS NOT NULL;

-- Drop old columns that are no longer used by EF
ALTER TABLE swap_requests DROP COLUMN IF EXISTS requester_person_id;
ALTER TABLE swap_requests DROP COLUMN IF EXISTS requester_slot_id;
ALTER TABLE swap_requests DROP COLUMN IF EXISTS target_slot_id;
ALTER TABLE swap_requests DROP COLUMN IF EXISTS message;
ALTER TABLE swap_requests DROP COLUMN IF EXISTS responded_at;

-- Drop old indexes that reference removed columns
DROP INDEX IF EXISTS idx_swap_requests_requester;
DROP INDEX IF EXISTS idx_swap_requests_target;

-- Create new indexes matching EF configuration
CREATE INDEX IF NOT EXISTS idx_swap_requests_status ON swap_requests (status, expires_at) WHERE status = 'Pending';
