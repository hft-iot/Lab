# Übung 12: Secure MQTT mit Client-Zertifikaten

## Ziel der Übung

- Den Unterschied zwischen offenem MQTT und Mutual TLS praktisch beobachten.
- Ein eigenes Client-Zertifikat für ein Device erzeugen.
- Einen MQTT-Client mit Zertifikat gegen den TLS-Listener des Brokers verbinden.
- Einen Rogue Client mit ungültigem Zertifikat scheitern sehen.

**Ausgangslage:**

Der Broker stellt in dieser Übung zwei Listener bereit:

- `1883`: offener Vergleichsfall ohne Authentisierung
- `8883`: TLS mit verpflichtendem Client-Zertifikat

Der MQTT Receiver bleibt als Beobachter auf dem offenen Listener und zeigt ankommende Telemetrie im Log. Dadurch lässt sich der Unterschied zwischen beiden Ansätzen direkt sehen, ohne auch den Receiver umbauen zu müssen.

**Zielbild:**

```text
Legitimer Sender --(gültiges Client-Zertifikat)--> Mosquitto:8883 --> MQTT Receiver
Rogue Sender -----(ungültiges Zertifikat)-------> Verbindung abgewiesen
```

---

## Schritt 1: Broker mit TLS neu starten

Für diese Übung sind die Server-Zertifikate bereits vorbereitet.

Prüfen Sie, dass der Broker weiterhin auf `1883` und zusätzlich auf `8883` erreichbar ist.

---

## Schritt 2: MQTT Receiver als Beobachter starten

Starten Sie den MQTT Receiver wie in den vorherigen Übungen.

Der Receiver abonniert weiterhin `temperature/living_room` und loggt jede eingehende Nachricht.

---

## Schritt 3: Rogue Client im offenen Setup beobachten

Im Projekt `src/MqttSenders` gibt es nun zusätzlich einen vorbereiteten Rogue Simulator. Er sendet absichtlich manipulierte Temperaturwerte zwischen `85` und `140` Grad.

Öffnen Sie `src/MqttSenders/appsettings.json` und ergänzen Sie den Rogue Simulator in der Senderliste:

```json
"senders": [
  "MQTT Simulator",
  "MQTT Rogue Simulator"
]
```

Lassen Sie zunächst beide Verbindungen auf dem offenen Broker:

```json
"MqttConnection": {
  "Host": "mqtt",
  "Port": 1883,
  "UseTls": false
},
"RogueConnection": {
  "Host": "mqtt",
  "Port": 1883,
  "UseTls": false
}
```

Starten Sie nun den Sender

Beobachten Sie im Receiver-Log:

- normale Telemetrie des legitimen Simulators
- zusätzlich offensichtlich falsche Werte des Rogue Clients

Damit ist sichtbar: Ohne Geräteidentität kann jedes beliebige Device Nachrichten einspeisen.

---

## Schritt 4: Eigenes Client-Zertifikat erzeugen

Für das eigentliche Device soll nun ein eigenes Client-Zertifikat erzeugt werden.

Die CA für diese Übung liegt bereits unter `.devcontainer/certs`. Das ist **nur für das Lab** so vorbereitet. In einer echten Umgebung würde der private CA-Schlüssel nicht an Devices verteilt.

Erzeugen Sie im Projekt `src/MqttSenders` ein eigenes Zertifikat:

```bash
mkdir -p certs
openssl genrsa -out certs/student-client.key 2048
openssl req -new -key certs/student-client.key -out certs/student-client.csr -subj "/CN=student-device"
printf 'extendedKeyUsage=clientAuth\n' > certs/client-ext.cnf
openssl x509 -req \
  -in certs/student-client.csr \
  -CA ../../.devcontainer/certs/lab-ca.crt \
  -CAkey ../../.devcontainer/certs/lab-ca.key \
  -CAcreateserial \
  -CAserial certs/student-client.srl \
  -out certs/student-client.crt \
  -days 365 \
  -sha256 \
  -extfile certs/client-ext.cnf
rm certs/student-client.csr certs/client-ext.cnf certs/student-client.srl
```

Danach liegen die für den legitimen Sender benötigten Dateien unter:

- `src/MqttSenders/certs/student-client.crt`
- `src/MqttSenders/certs/student-client.key`

---

## Schritt 5: Legitimen Sender auf Mutual TLS umstellen

Stellen Sie nun den legitimen Sender in `src/MqttSenders/appsettings.json` auf den TLS-Listener um:

```json
"MqttConnection": {
  "Host": "mqtt",
  "Port": 8883,
  "UseTls": true,
  "CaCertificatePath": "../../.devcontainer/certs/lab-ca.crt",
  "ClientCertificatePath": "certs/student-client.crt",
  "ClientKeyPath": "certs/student-client.key"
}
```

Der Sender soll jetzt:

- das Server-Zertifikat des Brokers prüfen
- das eigene Client-Zertifikat mitsenden
- weiterhin gültige Telemetrie publizieren

Starten Sie den Sender erneut und prüfen Sie im Log, dass die Verbindung auf `8883` aufgebaut wird.

---

## Schritt 6: Rogue auf TLS umstellen und scheitern sehen

Der Rogue Client besitzt bereits ein ungültiges Zertifikat. Dieses ist bewusst **nicht** von der Lab-CA signiert.

Stellen Sie nun auch die `RogueConnection` auf TLS um:

```json
"RogueConnection": {
  "Host": "mqtt",
  "Port": 8883,
  "UseTls": true,
  "CaCertificatePath": "../../.devcontainer/certs/lab-ca.crt",
  "ClientCertificatePath": "../../.devcontainer/certs/rogue-client.crt",
  "ClientKeyPath": "../../.devcontainer/certs/rogue-client.key"
}
```

Starten Sie `src/MqttSenders` erneut.

Beobachten Sie jetzt zwei Dinge:

- Der legitime Sender kann weiterhin Telemetrie an den Broker senden.
- Der Rogue Client loggt Verbindungsfehler und kann keine manipulierten Nachrichten mehr einspeisen.

Im Receiver sollten jetzt nur noch die normalen Temperaturwerte sichtbar sein.

---

## Bonus: Zertifikatsname sichtbar machen

In der Broker-Konfiguration ist `use_identity_as_username true` gesetzt. Recherchieren Sie, wie Mosquitto die Zertifikatsidentität intern verwendet und welche Möglichkeiten es gäbe, auf Basis dieser Identität weitere Autorisierungsregeln einzuziehen.
