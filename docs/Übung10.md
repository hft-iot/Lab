# Device Twin und Desired Properties

In dieser Übung wird die Cloud-to-Device-Kommunikation um Device Twin Desired Properties erweitert.
Der vorhandene `IoTHubConnector` soll dafür genutzt werden, um auf Änderungen an Desired Properties zu reagieren. Vergesst nicht euren IoT Hub Connection String in den appsettings.jsons zu aktualisieren, damit die Verbindung zum IoT Hub funktioniert.

## Ziel

Es soll eine Desired Property `temperature` verarbeitet werden.
Wenn sich diese ändert, soll der Connector die Änderung entgegennehmen, den Wert auf einem MQTT Topic publishen und eine Bestätigung als Reported Property vorbereiten.

## Aufgaben

1. Registriere im `IoTHubConnector.Connector` einen Callback für Desired Property Changes.
1. Bereite bei einer Änderung von `temperature` ein Objekt für die Bestätigung als Reported Property vor.
1. Die Bestätigung soll den gewünschten Wert, einen Status (`successful` oder `failed`) und ein Timestamp-Objekt enthalten.
1. Registriere im `MqttReceiver.MessageReceiver` den Event Handler für `ControlTemperatureReceived`.
1. Veröffentliche die gewünschte Temperatur im MQTT Topic `temperature/living_room/control`.

### Control Message Handler

1. Ergänze im Handler für `MessageReceived.ControlMessageReceived`, dass bei fehlender Temperatur per `GetTwinAsync` die Desired Properties gelesen werden sollen.
1. Falls keine `temperature` vorhanden ist, soll als Standardwert `20` verwendet werden.
