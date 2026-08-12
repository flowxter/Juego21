# Cliente Unity

Cliente de la mesa de blackjack. Unity 6 (`6000.3.4f1`).

## Cómo se conecta con el resto

Unity no consume paquetes NuGet, así que las dependencias llegan como DLL:

```
tools/UnityPlugins/          proyecto puente: referencia SignalR y los proyectos propios
        ↓ dotnet publish
tools/sync-unity-plugins.ps1 copia las DLL, omitiendo las que Unity ya trae
        ↓
client/BlackjackClient/Assets/Plugins/Blackjack/
```

`Blackjack.Core` y `Blackjack.Protocol` viajan igual que las demás. Por eso el
cliente comparte las reglas y los contratos con el servidor en vez de
reimplementarlos: `ActionValidator` decide qué botones se habilitan, y es el
mismo código que el servidor usa para rechazar comandos ilegales.

**Tras tocar `Blackjack.Core` o `Blackjack.Protocol` hay que volver a sincronizar**,
o Unity seguirá usando la versión anterior:

```bash
pwsh tools/sync-unity-plugins.ps1
```

## Puesta en marcha

1. Levanta base de datos y servidor de una vez:

   ```bash
   pwsh run.ps1
   ```

2. Sincroniza las DLL (solo la primera vez y tras cambiar Core o Protocol):

   ```bash
   pwsh tools/sync-unity-plugins.ps1
   ```

3. Abre `client/BlackjackClient` desde Unity Hub.

   La primera vez, el editor genera solo `Assets/Scenes/Mesa.unity` con la
   cámara y el cliente ya montados. Si hiciera falta rehacerla:
   **Blackjack → Regenerar escena de la mesa**.

4. Abre esa escena y dale a Play. Crea una cuenta, siéntate, apuesta y juega.

Para probar el multijugador de verdad hacen falta **dos cuentas distintas** en
dos instancias (por ejemplo el editor y un build de escritorio), sentadas en
asientos diferentes de la misma mesa.

## Comprobar que los scripts compilan sin abrir Unity

Unity solo informa de errores al abrir el proyecto, lo que hace lentísimo el
ciclo de corregir. Este proyecto compila los mismos ficheros contra las mismas
DLL, en segundos:

```bash
dotnet build tools/ClientCompileCheck
```

No forma parte de la solución principal a propósito: necesita Unity instalado
en la ruta esperada, y quien solo toque el servidor no debería verse obligado a
tenerlo.

## Qué hay y qué no

La interfaz actual es **provisional**, dibujada con IMGUI. Existe para validar
el protocolo de punta a punta —entrar, sentarse, apostar, pedir, doblar,
partir, cobrar— antes de invertir en la mesa de verdad.

Lo que ya está resuelto y se conserva cuando llegue el arte:

- `Net/GameConnection.cs` — SignalR, reconexión automática y reentrada en la mesa
- `Net/MainThreadDispatcher.cs` — traslada los callbacks al hilo principal de
  Unity, sin lo cual cualquier intento de tocar la escena reventaría
- `Net/AuthApi.cs` — registro y sesión

Lo que falta es exactamente la capa visual: fieltro, cartas, fichas,
animación de reparto y sonido.
