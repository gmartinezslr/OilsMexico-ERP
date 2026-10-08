// Detecta actividad del usuario en el cliente y notifica al circuito de Blazor (con throttle)
// para reiniciar el temporizador de inactividad de la sesión.
let _listeners = [];
let _dotNetRef = null;
let _ultimoEnvio = 0;
let _intervaloMs = 30000;

function _notificar() {
    const ahora = Date.now();
    if (ahora - _ultimoEnvio < _intervaloMs) return; // throttle
    _ultimoEnvio = ahora;
    if (_dotNetRef) _dotNetRef.invokeMethodAsync('RegistrarActividad').catch(() => { /* circuito caído */ });
}

export function iniciarMonitoreoActividad(dotNetRef, intervaloMs) {
    detenerMonitoreoActividad();
    _dotNetRef = dotNetRef;
    _intervaloMs = intervaloMs || 30000;
    _ultimoEnvio = 0;
    const eventos = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'wheel'];
    const opciones = { passive: true };
    _listeners = eventos.map(e => {
        document.addEventListener(e, _notificar, opciones);
        return [e, _notificar];
    });
}

export function detenerMonitoreoActividad() {
    _listeners.forEach(([e, fn]) => document.removeEventListener(e, fn));
    _listeners = [];
    _dotNetRef = null;
}

