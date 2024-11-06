-- Table : authz.Decision
CREATE TABLE IF NOT EXISTS authz.decision
(
	decisionid integer PRIMARY KEY,
	name text,
	description text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authz.decision TO "${APP-USER}";

GRANT ALL ON TABLE authz.decision TO "${YUNIQL-USER}";

INSERT INTO authz.decision(
	decisionid, name, description)
	VALUES (1, 'Permit', 'the requested access is permitted');
INSERT INTO authz.decision(
	decisionid, name, description)
	VALUES (2, 'Deny', 'the requested access is denied');
INSERT INTO authz.decision(
	decisionid, name, description)
	VALUES (3, 'Indeterminate', 'the PDP is unable to evaluate the requested access.  Reasons for such inability include: missing attributes, network errors while retrieving policies, division by zero during policy evaluation, syntax errors in the decision request or in the policy, etc.');
INSERT INTO authz.decision(
	decisionid, name, description)
	VALUES (4, 'NotApplicable', 'the PDP does not have any policy that applies to this decision request.');

-- Table: authz.eventlog
CREATE TABLE IF NOT EXISTS authz.eventlog
(
	sessionid text,
	created timestamp with time zone NOT NULL,
	subjectuserid INTEGER,
	subjectorgcode text,
	subjectorgnumber INTEGER,
	subjectparty INTEGER,
	resourcepartyid INTEGER,
	resource text,
	instanceid text,
	operation text,
	ipaddress text,
	contextrequestjson jsonb,
	decision integer,
	CONSTRAINT authzdecisionid_fkey FOREIGN KEY (decision)
        REFERENCES authz.decision (decisionid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authz.eventlog TO "${APP-USER}";

GRANT ALL ON TABLE authz.eventlog TO "${YUNIQL-USER}";
