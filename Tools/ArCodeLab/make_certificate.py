"""Create a local development CA and LAN server certificate. Never installs trust."""
import datetime as dt
import ipaddress
import json
from pathlib import Path
import sys
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives.serialization import pkcs12
from cryptography.x509.oid import NameOID, ExtendedKeyUsageOID

ip, folder = sys.argv[1], Path(sys.argv[2])
ipaddress.ip_address(ip)
folder.mkdir(parents=True, exist_ok=True)
if (folder/'format.txt').exists() and (folder/'format.txt').read_text() == 'mono-v2' and (folder/'ip.txt').read_text() == ip:
    cert = x509.load_der_x509_certificate((folder/'root.cer').read_bytes())
    if cert.not_valid_after_utc > dt.datetime.now(dt.timezone.utc) + dt.timedelta(hours=1):
        sys.exit(0)
now=dt.datetime.now(dt.timezone.utc)
root_key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
name=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'AI-ON Local AR Development CA')])
root=(x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(root_key.public_key())
    .serial_number(x509.random_serial_number()).not_valid_before(now-dt.timedelta(minutes=5))
    .not_valid_after(now+dt.timedelta(days=7)).add_extension(x509.BasicConstraints(ca=True,path_length=0),True)
    .add_extension(x509.KeyUsage(False,False,False,False,False,True,True,False,False),True)
    .add_extension(x509.SubjectKeyIdentifier.from_public_key(root_key.public_key()),False)
    .sign(root_key,hashes.SHA256()))
key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
leaf=(x509.CertificateBuilder().subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'AI-ON Local AR')]))
    .issuer_name(name).public_key(key.public_key()).serial_number(x509.random_serial_number())
    .not_valid_before(now-dt.timedelta(minutes=5)).not_valid_after(now+dt.timedelta(days=7))
    .add_extension(x509.BasicConstraints(ca=False,path_length=None),True)
    .add_extension(x509.AuthorityKeyIdentifier.from_issuer_public_key(root_key.public_key()),False)
    .add_extension(x509.SubjectKeyIdentifier.from_public_key(key.public_key()),False)
    .add_extension(x509.SubjectAlternativeName([x509.IPAddress(ipaddress.ip_address(ip)),x509.IPAddress(ipaddress.ip_address('127.0.0.1')),x509.DNSName('localhost')]),False)
    .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH]),False)
    .add_extension(x509.KeyUsage(True,False,True,False,False,False,False,False,False),True)
    .sign(root_key,hashes.SHA256()))
# A random password is kept only alongside the private key in ignored Library/, never served.
import secrets
password=secrets.token_urlsafe(24)
encryption=(serialization.PrivateFormat.PKCS12.encryption_builder().kdf_rounds(50000)
            .key_cert_algorithm(pkcs12.PBES.PBESv1SHA1And3KeyTripleDESCBC)
            .hmac_hash(hashes.SHA1()).build(password.encode()))
(folder/'server.pfx').write_bytes(pkcs12.serialize_key_and_certificates(b'local-ar',key,leaf,[root],encryption))
(folder/'password.txt').write_text(password)
(folder/'root.cer').write_bytes(root.public_bytes(serialization.Encoding.DER))
(folder/'ip.txt').write_text(ip)
(folder/'format.txt').write_text('mono-v2')
print('Local AR certificate ready; trust was not installed.')
