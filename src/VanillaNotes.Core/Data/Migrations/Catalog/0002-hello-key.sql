-- The key of a profile wrapped by Windows Hello, when the person turned it on for the profile.
-- Documented in docs/02-data/: change both together.

ALTER TABLE profiles ADD COLUMN hello_key TEXT CHECK (hello_key IS NULL OR password_key IS NOT NULL);
