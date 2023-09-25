-- Procedure: create_authorizationevent
CREATE OR REPLACE FUNCTION authz.create_authorizationevent(
	_subjectuserid text,
	_subjectorgcode text,
	_subjectorgnumber text,
	_subjectparty text,
	_resourcepartyid text,
	_resource text,
	_instanceid text,
	_operation text,
	_timetodelete text,
	_ipadress text,
	_contextrequestjson jsonb,
	_decision text)
    RETURNS authz.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authz.eventlog(
	created,
	subjectuserid,
	subjectorgcode,
	subjectorgnumber,
	subjectparty,
	resourcepartyid,
	resource,
	instanceid,
	operation,
	timetodelete,
	ipadress,
	contextrequestjson,
	decision
)
VALUES (
	Now(),
	_subjectuserid,
	_subjectorgcode,
	_subjectorgnumber,
	_subjectparty,
	_resourcepartyid,
	_resource,
	_instanceid,
	_operation,
	_timetodelete,
	_ipadress,
	_contextrequestjson,
	_decision
)
RETURNING *;
$BODY$;