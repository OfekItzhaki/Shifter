-- Client-ready portability/customer-hosted schema alignment.
-- Mirrors EF migrations for organization portability, special days, space
-- self-service defaults, and attendance records in the SQL migration path.

CREATE TABLE IF NOT EXISTS organizations (
  id UUID PRIMARY KEY,
  display_name VARCHAR(200) NOT NULL,
  normalized_name VARCHAR(200) NOT NULL,
  primary_owner_user_id UUID NOT NULL,
  country_code VARCHAR(2),
  setup_template VARCHAR(80),
  default_locale VARCHAR(12),
  default_timezone_id VARCHAR(100),
  status TEXT NOT NULL DEFAULT 'Active',
  relocated_at TIMESTAMPTZ,
  disabled_at TIMESTAMPTZ,
  purge_eligible_at TIMESTAMPTZ,
  dedicated_deployment_key VARCHAR(120),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE spaces ADD COLUMN IF NOT EXISTS organization_id UUID;

INSERT INTO organizations (
  id,
  display_name,
  normalized_name,
  primary_owner_user_id,
  country_code,
  setup_template,
  default_locale,
  status,
  created_at,
  updated_at
)
SELECT
  s.id,
  CASE
    WHEN u.country_code IS NOT NULL AND btrim(u.country_code) <> ''
      THEN upper(btrim(u.country_code)) || ' General'
    ELSE s.name
  END,
  upper(CASE
    WHEN u.country_code IS NOT NULL AND btrim(u.country_code) <> ''
      THEN upper(btrim(u.country_code)) || ' General'
    ELSE s.name
  END),
  s.owner_user_id,
  upper(nullif(btrim(u.country_code), '')),
  'general',
  s.locale,
  'Active',
  s.created_at,
  s.updated_at
FROM spaces s
LEFT JOIN users u ON u.id = s.owner_user_id
WHERE NOT EXISTS (
  SELECT 1 FROM organizations o WHERE o.id = s.id
);

UPDATE spaces
SET organization_id = id
WHERE organization_id IS NULL;

ALTER TABLE spaces ALTER COLUMN organization_id SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_spaces_organization_id ON spaces(organization_id);
CREATE INDEX IF NOT EXISTS idx_organizations_country_template ON organizations(country_code, setup_template);
CREATE INDEX IF NOT EXISTS idx_organizations_normalized_name ON organizations(normalized_name);
CREATE INDEX IF NOT EXISTS idx_organizations_primary_owner_user_id ON organizations(primary_owner_user_id);
CREATE INDEX IF NOT EXISTS idx_organizations_purge_eligible_at ON organizations(purge_eligible_at);
CREATE INDEX IF NOT EXISTS idx_organizations_status ON organizations(status);

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_spaces_organizations_organization_id'
  ) THEN
    ALTER TABLE spaces
      ADD CONSTRAINT fk_spaces_organizations_organization_id
      FOREIGN KEY (organization_id) REFERENCES organizations(id)
      ON DELETE RESTRICT;
  END IF;
END $$;

ALTER TABLE self_service_configs
  ADD COLUMN IF NOT EXISTS max_absences_per_cycle INTEGER NOT NULL DEFAULT 3,
  ADD COLUMN IF NOT EXISTS allow_member_shift_claims BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN IF NOT EXISTS allow_waitlist BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN IF NOT EXISTS allow_shift_change_requests BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN IF NOT EXISTS allow_absence_reports BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN IF NOT EXISTS allow_shift_swaps BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE shift_slots
  ADD COLUMN IF NOT EXISTS starts_at TIMESTAMPTZ NOT NULL DEFAULT '0001-01-01 00:00:00+00',
  ADD COLUMN IF NOT EXISTS ends_at TIMESTAMPTZ NOT NULL DEFAULT '0001-01-01 00:00:00+00';

UPDATE shift_slots
SET
  starts_at = ((date + start_time) AT TIME ZONE 'UTC'),
  ends_at = ((date + end_time + CASE WHEN end_time <= start_time THEN INTERVAL '1 day' ELSE INTERVAL '0 day' END) AT TIME ZONE 'UTC')
WHERE date IS NOT NULL
  AND start_time IS NOT NULL
  AND end_time IS NOT NULL
  AND (starts_at = '0001-01-01 00:00:00+00' OR ends_at = '0001-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS organization_subscriptions (
  id UUID PRIMARY KEY,
  organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  billing_mode TEXT NOT NULL,
  tier_id TEXT NOT NULL,
  status TEXT NOT NULL,
  provider_subscription_id TEXT,
  provider_customer_id TEXT,
  current_period_start TIMESTAMPTZ NOT NULL,
  current_period_end TIMESTAMPTZ,
  covered_space_limit INTEGER,
  covered_member_limit INTEGER,
  auto_renew BOOLEAN NOT NULL,
  canceled_at TIMESTAMPTZ,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_organization_subscriptions_status ON organization_subscriptions(status);
CREATE UNIQUE INDEX IF NOT EXISTS uq_organization_subscriptions_organization_id ON organization_subscriptions(organization_id);

CREATE TABLE IF NOT EXISTS space_special_days (
  id UUID PRIMARY KEY,
  space_id UUID NOT NULL,
  date DATE NOT NULL,
  name VARCHAR(120) NOT NULL,
  kind INTEGER NOT NULL,
  home_leave_weight_multiplier NUMERIC(3,2) NOT NULL DEFAULT 1.00,
  requires_coverage BOOLEAN NOT NULL DEFAULT FALSE,
  is_auto_generated BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_space_special_days_space_date_name ON space_special_days(space_id, date, name);
ALTER TABLE space_special_days ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON space_special_days;
CREATE POLICY tenant_isolation ON space_special_days
  USING (space_id::text = current_setting('app.current_space_id', TRUE));

CREATE TABLE IF NOT EXISTS space_self_service_defaults (
  id UUID PRIMARY KEY,
  space_id UUID NOT NULL,
  min_shifts_per_cycle INTEGER NOT NULL DEFAULT 0,
  max_shifts_per_cycle INTEGER NOT NULL DEFAULT 7,
  request_window_open_offset_hours INTEGER NOT NULL DEFAULT 168,
  request_window_close_offset_hours INTEGER NOT NULL DEFAULT 24,
  cancellation_cutoff_hours INTEGER NOT NULL DEFAULT 24,
  max_absences_per_cycle INTEGER NOT NULL DEFAULT 3,
  max_late_cancellations_per_cycle INTEGER NOT NULL DEFAULT 2,
  late_cancellation_window_hours INTEGER NOT NULL DEFAULT 24,
  waitlist_offer_minutes INTEGER NOT NULL DEFAULT 60,
  cycle_duration_days INTEGER NOT NULL DEFAULT 7,
  allow_member_shift_claims BOOLEAN NOT NULL DEFAULT TRUE,
  allow_waitlist BOOLEAN NOT NULL DEFAULT TRUE,
  allow_shift_change_requests BOOLEAN NOT NULL DEFAULT TRUE,
  allow_absence_reports BOOLEAN NOT NULL DEFAULT TRUE,
  allow_shift_swaps BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_space_self_service_defaults_space_id ON space_self_service_defaults(space_id);

CREATE TABLE IF NOT EXISTS shift_attendance_records (
  id UUID PRIMARY KEY,
  space_id UUID NOT NULL,
  group_id UUID NOT NULL,
  scheduling_cycle_id UUID NOT NULL,
  shift_request_id UUID NOT NULL,
  shift_slot_id UUID NOT NULL,
  person_id UUID NOT NULL,
  status TEXT NOT NULL,
  note VARCHAR(500),
  recorded_by_user_id UUID NOT NULL,
  recorded_at TIMESTAMPTZ NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_shift_attendance_records_group_cycle_status
  ON shift_attendance_records(group_id, scheduling_cycle_id, status);
CREATE INDEX IF NOT EXISTS idx_shift_attendance_records_person_cycle_status
  ON shift_attendance_records(person_id, scheduling_cycle_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS idx_shift_attendance_records_shift_request
  ON shift_attendance_records(shift_request_id);

ALTER TABLE shift_attendance_records ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON shift_attendance_records;
CREATE POLICY tenant_isolation ON shift_attendance_records
  USING (space_id::text = current_setting('app.current_space_id', TRUE));
