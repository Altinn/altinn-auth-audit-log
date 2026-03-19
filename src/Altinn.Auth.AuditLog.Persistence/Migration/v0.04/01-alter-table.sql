-- Add new authentication methods
INSERT INTO authentication.authenticationmethod (authenticationmethodid, name, description)
VALUES (21, 'MinIDTOTP', 'MinIDTOTP')
ON CONFLICT (authenticationmethodid) DO NOTHING;

INSERT INTO authentication.authenticationmethod (authenticationmethodid, name, description)
VALUES (22, 'IdportenEpost', 'IdportenEpost')
ON CONFLICT (authenticationmethodid) DO NOTHING;

