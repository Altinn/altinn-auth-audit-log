-- Table: authentication.eventlog
CREATE TABLE IF NOT EXISTS authentication.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	userid text,
	supplierid text,
	eventtype text,
	orgnumber text,
	authenticationmethod text,
	authenticationlevel text,
	sessionid text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog_admin;
