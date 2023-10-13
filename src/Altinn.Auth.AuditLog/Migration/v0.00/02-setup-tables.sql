-- Table: authentication.eventtype
CREATE TABLE IF NOT EXISTS authentication.authenticationeventtype
(
	authenticationeventtypeid integer PRIMARY KEY,
	name text,
	description text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.authenticationeventtype TO auth_auditlog;

GRANT ALL ON TABLE authentication.authenticationeventtype TO auth_auditlog_admin;

INSERT INTO authentication.authenticationeventtype(
	authenticationeventtypeid, name, description)
	VALUES (1, 'Authenticate', 'Authenticate');
INSERT INTO authentication.authenticationeventtype(
	authenticationeventtypeid, name, description)
	VALUES (2, 'Refresh', 'Refresh');
INSERT INTO authentication.authenticationeventtype(
	authenticationeventtypeid, name, description)
	VALUES (3, 'TokenExchange', 'TokenExchange');
INSERT INTO authentication.authenticationeventtype(
	authenticationeventtypeid, name, description)
	VALUES (4, 'Logout', 'Logout');

-- Table: authentication.authenticationmethod
CREATE TABLE IF NOT EXISTS authentication.authenticationmethod
(
	authenticationmethodid integer PRIMARY KEY,
	name text,
	description text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.authenticationmethod TO auth_auditlog;

GRANT ALL ON TABLE authentication.authenticationmethod TO auth_auditlog_admin;

INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (-1, 'NotDefined', 'NotDefined');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (0, 'AltinnPIN', 'AltinnPIN');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (1, 'BankID', 'BankID');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (2, 'BuyPass', 'BuyPass');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (3, 'SAML2', 'SAML2');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (4, 'SMSPIN', 'SMSPIN');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (5, 'StaticPassword', 'StaticPassword');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (6, 'TaxPIN', 'TaxPIN');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (7, 'FederationNotUsedAnymore', 'FederationNotUsedAnymore');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (8, 'SelfIdentified', 'SelfIdentified');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (9, 'EnterpriseIdentified', 'EnterpriseIdentified');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (10, 'Commfides', 'Commfides');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (11, 'MinIDPin', 'MinIDPin');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (12, 'OpenSshIdentified', 'OpenSshIdentified');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (13, 'EIDAS', 'EIDAS');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (14, 'BankIDMobil', 'BankIDMobil');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (15, 'MinIDOTC', 'MinIDOTC');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (16, 'MaskinPorten', 'MaskinPorten');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (17, 'VirksomhetsBruker', 'VirksomhetsBruker');
INSERT INTO authentication.authenticationmethod(
	authenticationmethodid, name, description)
	VALUES (18, 'TestID', 'TestID');



-- Table: authentication.authenticationlevel
CREATE TABLE IF NOT EXISTS authentication.authenticationlevel
(
	authenticationlevelid integer PRIMARY KEY,
	name text,
	description text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.authenticationlevel TO auth_auditlog;

GRANT ALL ON TABLE authentication.authenticationlevel TO auth_auditlog_admin;

INSERT INTO authentication.authenticationlevel(
	authenticationlevelid, name, description)
	VALUES (0, 'SelfIdentified','SelfIdentified');
INSERT INTO authentication.authenticationlevel(
	authenticationlevelid, name, description)
	VALUES (1, 'NotSensitive','NotSensitive');
INSERT INTO authentication.authenticationlevel(
	authenticationlevelid, name, description)
	VALUES (2, 'QuiteSensitive', 'QuiteSensitive');
INSERT INTO authentication.authenticationlevel(
	authenticationlevelid, name, description)
	VALUES (3, 'Sensitive', 'Sensitive');
INSERT INTO authentication.authenticationlevel(
	authenticationlevelid, name, description)
	VALUES (4, 'VerySensitive', 'VerySensitive');

-- Table: authentication.eventlog
CREATE TABLE IF NOT EXISTS authentication.eventlog
(
	sessionid text,
	created timestamp with time zone NOT NULL,
	userid integer,
	supplierid text,
	orgnumber integer,
	eventtypeid integer,
	authenticationmethodid integer,
	authenticationlevelid integer,
	ipaddress text,
	isauthenticated boolean NOT NULL,
	CONSTRAINT authenticationeventtype_fkey FOREIGN KEY (eventtypeid)
        REFERENCES authentication.authenticationeventtype (authenticationeventtypeid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    CONSTRAINT authenticationlevel_fkey FOREIGN KEY (authenticationlevelid)
        REFERENCES authentication.authenticationlevel (authenticationlevelid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    CONSTRAINT authenticationmethod_fkey FOREIGN KEY (authenticationmethodid)
        REFERENCES authentication.authenticationmethod (authenticationmethodid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog_admin;