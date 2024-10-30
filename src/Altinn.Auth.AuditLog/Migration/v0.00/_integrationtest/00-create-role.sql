DO $$ BEGIN
    CREATE ROLE auth_auditlog;
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;
