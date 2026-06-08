# Übung 11: Remote Update mit hawkBit

## Ziel der Übung

- Einen Update-Server mit hawkBit verwenden.
- Ein Device als Target registrieren.
- Ein Rollout mit Distribution Set und Software Module vorbereiten.
- Den MQTT Receiver als einfachen Device Update Client gegen die Controller API anbinden.

**Ausgangslage:**

Der MQTT Receiver läuft bereits als Worker Service. In dieser Übung wird er erweitert, damit er regelmäßig die hawkBit Controller API abfragt.

**Zielbild:**

```text
MQTT Receiver --> hawkBit Controller API --> Deployment Details
```

---

## Schritt 1: hawkBit öffnen

Öffnen Sie die hawkBit UI unter `http://localhost:8088/`.

Anmeldedaten:

- Benutzer: `admin`
- Passwort: `admin`

---

## Schritt 2: Target anlegen

Jedes Device, das ein Update bekommen soll, wird in hawkBit als Target angelegt.

1. Öffnen Sie im Menü `Targets`.
2. Klicken Sie auf `+` unten rechts.
3. Tragen Sie als `Controller Id` den Wert `test-device` ein.

---

## Schritt 3: Software Module anlegen

Ein Remote Update besteht später aus einem Rollout. Das Rollout verweist auf ein Distribution Set, und das Distribution Set enthält ein oder mehrere Software Modules. Das Software Module beschreibt die eigentlichen Artefakte des Updates, zum Beispiel Binaries oder ein OS-Image.

1. Öffnen Sie im Menü `Software Modules`.
2. Klicken Sie auf `+` unten rechts.
3. Verwenden Sie folgende Werte:

| Feld | Wert |
| :--- | :--- |
| Type | `Application` |
| Name | `Test Deployment` |
| Version | `1.0` |

1. Klicken Sie auf `Create`.
2. Laden Sie bei `Add Artifacts` eine beliebige Datei hoch, am besten eine leere JSON-Datei.

Hinweis: Falls die Meldung `Upload failed, please try again.` erscheint, verwenden Sie einen anderen Dateityp.

---

## Schritt 4: Distribution Set anlegen

1. Öffnen Sie im Menü `Distribution Sets`.
2. Klicken Sie auf `+`.
3. Verwenden Sie folgende Werte:

| Feld | Wert |
| :--- | :--- |
| Type | `App(s) only` |
| Name | `Test Deployment` |
| Version | `1.0` |

1. Klicken Sie auf `Create`.
2. Wählen Sie bei `Add Software Modules` das Software Module `Test Deployment` aus und fügen Sie es hinzu.
3. Klicken Sie auf `Finish`.

---

## Schritt 5: Target Filter anlegen

1. Öffnen Sie erneut das Menü `Targets`.
2. Klicken Sie rechts oben auf `Toggle Search`.
3. Tragen Sie als `Raw Filter` den Ausdruck `name == *` ein.
4. Klicken Sie auf `+ Save`.
5. Verwenden Sie als Namen `Test Devices`.
6. Klicken Sie auf `Save`.

---

## Schritt 6: Rollout erstellen

1. Öffnen Sie das Menü `Rollouts`.
2. Klicken Sie auf `+`.
3. Verwenden Sie folgende Werte:

| Feld | Wert |
| :--- | :--- |
| Name | `Test Deployment` |
| Distribution Set | `Test Deployment` |
| Target Filter | `Test Devices` |

1. Klicken Sie auf `Create`.

---

## Schritt 7: Target-Token aktivieren

Der Device Update Client authentifiziert sich mit einem Target-Token.

1. Öffnen Sie im hawkBit-Menü `Config`.
2. Aktivieren Sie `authentication.targettoken.enabled`.
3. Klicken Sie auf `Save`.
4. Öffnen Sie nun das Target `test-device`.
5. Klicken Sie auf `Details` und kopieren Sie den `Security Token`.

---

## Schritt 8: MQTT Receiver konfigurieren

Konfigurieren Sie nun den MQTT Receiver so, dass er sich mit der hawkBit Controller API verbinden kann.

Verwenden Sie im Projekt `src/MqttReceiver` in der Datei `appsettings.json` folgende Einstellungen:

| Schlüssel | Bedeutung |
| :--- | :--- |
| `UpdateControllerId` | Die Controller-ID des Targets, hier `test-device` |
| `UpdateControllerToken` | Das hawkBit Target-Token |

Wenn `UpdateControllerToken` leer bleibt, soll der Update-Client nicht starten und stattdessen nur eine Warnung loggen.

---

## Schritt 9: Device Update Client beobachten

Starten Sie den MQTT Receiver. Der Worker pollt die Controller API alle 5 Sekunden unter folgendem Schema:

```text
http://localhost:8080/default/controller/v1/<deviceID>
```

Dabei werden folgende Header verwendet:

- `Accept: application/hal+json`
- `Authorization: TargetToken <token>`

Wenn eine Antwort mit Status `200 OK` zurückkommt und im Feld `_links` ein `deploymentBase` vorhanden ist, werden die Deployment-Details geladen und als Zusammenfassung im Log ausgegeben.

So lässt sich nachvollziehen, ob für das Device gerade ein Deployment bereitsteht.
