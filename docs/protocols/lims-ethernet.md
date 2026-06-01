# LIMS Ethernet — protocolo de comunicación de equipos PAC

## Propósito de este documento

Documentar el wire format del protocolo "LIMS Ethernet" que los equipos PAC
(OptiPMD, OptiDist, OptiCPP, OptiFZP, etc.) emiten cuando el operador activa
en el firmware del equipo la opción de salida **"LIMS Ethernet"**.

Esta documentación es necesaria porque PAC L.P. no publica la especificación
del protocolo en ningún canal técnico accesible. Cualquier integrador que
quiera consumir los datos que el equipo emite vía esa opción de output
necesita conocer el formato.

## Origen de la especificación

La especificación de este documento fue derivada por **observación pasiva
de tráfico de red** en la red propia del laboratorio cliente, utilizando
Wireshark sobre los datagramas que intercambia el equipo PAC con el software
existente del cliente cuando el equipo está configurado en modo "LIMS Ethernet".

No hubo desensamble, decompilación ni análisis de firmware del equipo PAC.
No hubo modificación del software propietario existente. La técnica empleada
es ingeniería de protocolo black-box estándar, equivalente a la que se realiza
para documentar cualquier protocolo de red no publicado entre dos sistemas
que operan sobre infraestructura del cliente.

## Marco legal

La documentación de este protocolo, así como su implementación en colectores
de terceros para fines de interoperabilidad con el equipo PAC propiedad del
cliente, está amparada por:

- **Directiva UE 2009/24/CE, artículo 6** — excepción explícita de
  descompilación e ingeniería inversa para lograr interoperabilidad cuando
  el titular no provee la información necesaria por canales accesibles.
- **DMCA §1201(f)** (EUA) — excepción para reverse engineering por
  interoperabilidad.
- **Jurisprudencia Sega v. Accolade (1992)** y **Sony v. Connectix (2000)** —
  fair use sobre información funcional de interfaz, no protegible como
  expresión creativa.

La información derivada se limita estrictamente a lo necesario para que el
colector dialogue con el equipo: bytes intercambiados, puertos, semántica
mínima de campos. No se replica ni se extrae código del software propietario
existente.

El wire format en sí —tres bytes binarios de beacon y un string ASCII de
ACK— no contiene algoritmos, cifrado ni elementos creativos protegibles. Es
información funcional de interfaz, equivalente a saber qué voltaje espera un
pin de un componente de hardware: condición necesaria para interoperar.

## Especificación del protocolo

### Topología y roles

El protocolo es de tipo "discovery + sesión", con dos planos desacoplados:

- **Plano de control** (UDP, puerto 3000): el equipo se anuncia, el colector
  responde con sus coordenadas TCP.
- **Plano de datos** (TCP, puerto del ACK, típicamente 9980): el equipo
  inicia la conexión y envía el resultado del ensayo serializado en JSON.

El **equipo PAC** es el lado activo del descubrimiento (envía beacon y abre
TCP). El **colector** (server LIMS) es pasivo: bindea ambos puertos y espera.

### Secuencia completa

```
┌─────────────┐                                  ┌─────────────────┐
│  Equipo PAC │                                  │ Colector (LIMS) │
│             │                                  │ bind UDP 3000    │
│             │                                  │ bind TCP 9980    │
│             │                                  │                  │
│ start ensayo│                                  │                  │
│             │ ─── beacon UDP :3000 ──────────► │                  │
│             │     [0x01, 0x02, 0x03]           │                  │
│             │                                  │                  │
│             │ ◄─── ACK UDP unicast ──────────  │                  │
│             │      "ACK <ip> <port>"           │                  │
│             │                                  │                  │
│             │ ─── TCP SYN → <ip>:<port> ─────► │                  │
│             │ ──── JSON payload + \0 ────────► │                  │
│             │                                  │                  │
│             │ ◄─── JSON response ────────────  │                  │
│             │      {Error, SaveCheckSum}       │                  │
│             │                                  │                  │
│             │ ─── TCP FIN ───────────────────► │                  │
└─────────────┘                                  └─────────────────┘
```

### 1. Beacon (UDP)

- **Transporte**: UDP
- **Puerto destino**: 3000
- **Dirección**: típicamente broadcast a la subred del lab; algunos firmwares
  permiten dirigirlo a una IP fija.
- **Payload exacto**: 3 bytes binarios — `0x01 0x02 0x03`
- **Cadencia**: enviado al iniciar el ensayo y, según firmware, repetido cada
  ~5–10 s hasta recibir ACK válido.

### 2. ACK (UDP unicast)

- **Transporte**: UDP
- **Puerto destino**: el puerto efímero desde el que el equipo envió el beacon
  (extraído del `addr` del recvfrom).
- **Payload**: string ASCII con tres tokens separados por espacio:

  ```
  ACK <server_ip> <tcp_port>
  ```

  Ejemplo:
  ```
  ACK 192.168.100.50 9980
  ```

  Codificación: UTF-8 plano (es ASCII de hecho — no hay caracteres fuera del
  rango 0–127). Sin null terminator, sin `\r\n`, sin length prefix. La
  longitud del payload la da el header UDP.

  Hexadecimal del ejemplo anterior (23 bytes):
  ```
  41 43 4B 20 31 39 32 2E 31 36 38 2E 31 30 30 2E
  35 30 20 39 39 38 30
  ```

### 3. Sesión TCP

- **Transporte**: TCP
- **Iniciador**: el equipo PAC abre la conexión hacia `<server_ip>:<tcp_port>`
  indicados en el ACK.
- **Payload del equipo → colector**: JSON UTF-8 terminado con un byte `0x00`
  (NUL). El equipo cierra la mitad write de la conexión tras enviarlo.
- **Forma del JSON**: estructura `DataDictionary` con campos del ensayo.
  Ejemplo OptiPMD:

  ```json
  {
    "AnalyzerType": "OptiPMD",
    "DataDictionary": {
      "AnalyzerSerialNumber": "1216",
      "SampleIdentifier": "IRAM 2 2024",
      "OperatorId": "Fer",
      "ProgramName": "ASTM D7345",
      "StartRunDate": "25 Apr 2026",
      "StartRunTime": "21:08",
      "IBP": "151.5",
      "FBP": "260.7",
      "Recovery": "98.1",
      "Residue": "1.3",
      "Recovered_0005": "167.0",
      "Recovered_0010": "174.0",
      "...": "..."
    }
  }
  ```

  Los valores son strings; los numéricos vienen sin formato fijo. Los puntos
  de la curva van como claves `Recovered_{pct:04}`.

### 4. Respuesta del colector

- **Payload del colector → equipo**: JSON UTF-8 con dos campos:

  ```json
  {"Error":"","SaveCheckSum":"abcd1234"}
  ```

  - `Error`: string vacío `""` si el procesamiento fue OK, `"NACK"` si hubo
    error.
  - `SaveCheckSum`: hex checksum del payload guardado (usado por el equipo
    para confirmar persistencia downstream).

### 5. Cierre

Tras recibir la respuesta, el equipo cierra la conexión TCP (FIN). El ciclo
puede repetirse para múltiples ensayos.

## Implementación canónica

La implementación de referencia del **lado equipo** del protocolo vive en el
repositorio del colector como una herramienta CLI (`pac-mock`):

- `pac-mock lims send --target <ip> --json <file>`

Ese código ejecuta los 5 pasos anteriores y constituye la documentación
ejecutable del protocolo. Si este documento .md diverge del código de
`pac-mock`, el código es la fuente de verdad — este archivo se actualiza.

Adicionalmente, la implementación del **lado colector** (server LIMS) está
en `src/PacCollector.Infrastructure/Network/UdpServer.cs` y `TcpServer.cs`.

## Notas operativas

- No hay autenticación. La seguridad del canal depende del segmento de red
  del laboratorio (típicamente VLAN aislada para instrumentación).
- No hay cifrado. El JSON viaja en claro por TCP.
- Sin handshake de versión: si PAC modifica el wire format en una firmware
  update, el colector necesita actualización. El fallback "Print over
  Ethernet" (ver [print-ipp.md](./print-ipp.md)) mitiga este riesgo.
- El puerto TCP no está fijado por el protocolo; viaja en el ACK. El colector
  puede usar cualquier puerto disponible (9980 es el default por
  configuración, no por especificación).
