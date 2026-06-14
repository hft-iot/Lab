# TLS-Zertifikate fuer Uebung 12

Dieses Verzeichnis enthaelt vorbereitete Zertifikate fuer das Lab zu MQTT mit Mutual TLS.

## Enthaltene Dateien

- `lab-ca.crt`: Root-CA, der der Broker vertraut
- `lab-ca.key`: Privater Schluessel der Lab-CA, nur fuer diese Uebung im Repo enthalten
- `mqtt-server.crt`: Server-Zertifikat fuer den Mosquitto-Broker
- `mqtt-server.key`: Privater Schluessel des Brokers
- `rogue-client.crt`: Absichtlich ungueltiges, selbstsigniertes Client-Zertifikat
- `rogue-client.key`: Privater Schluessel des Rogue Clients

## Hinweis

Der private CA-Schluessel liegt hier bewusst nur fuer Lehrzwecke. In einer realen IoT-Umgebung duerfte ein Device niemals direkten Zugriff auf den CA-Schluessel haben.
