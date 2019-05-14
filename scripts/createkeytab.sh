instructions()
{
	echo "SYNTAX: createkeytab.sh <keytab> <PRINCIPAL> <password> [<KVNO>]"
	echo "  PRINCIPAL FORMAT: http/mysite.atsome.url@DOMAIN.COM"
	echo "  DOMAIN.COM must match what is declared as default_realm in krb5.ini"
	echo "  KVNO default is 2"
	return 0;
}


FILE=$1
PRINCIPAL=$2
PASSWORD=$3
KVNO=$4
if [ -z "$FILE" ] || [ -z "$PRINCIPAL" ] || [ -z "$PASSWORD" ] 
then
	instructions
	exit
fi
if [ -z "$KVNO" ]
then
	KVNO=2
fi
ktutil <<EOF
addent -password -p $PRINCIPAL -k $KVNO -e aes256-cts-hmac-sha1-96
$PASSWORD
addent -password -p $PRINCIPAL -k $KVNO -e aes128-cts-hmac-sha1-96
$PASSWORD
addent -password -p $PRINCIPAL -k $KVNO -e rc4-hmac
$PASSWORD
write_kt $FILE
EOF
