import { dotnet } from './_framework/dotnet.js';

async function initialize() {
    const canvas = document.getElementById('canvas');

    const originalGetContext = canvas.getContext.bind(canvas);
    canvas.getContext = function(type, attrs) {
        if (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl') {
            return originalGetContext(type, {
                ...attrs,
                alpha: false
            });
        }

        return originalGetContext(type, attrs);
    };

    const { getAssemblyExports, getConfig, runMain } = await dotnet
        .withDiagnosticTracing(false)
        .create();

    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    dotnet.instance.Module.canvas = canvas;
    let lastLogicalWidth = 0;
    let lastLogicalHeight = 0;
    let lastPixelWidth = 0;
    let lastPixelHeight = 0;

    function resizeCanvasToDisplaySize() {
        const dpr = Math.min(window.devicePixelRatio || 1, 2);
        const rect = canvas.getBoundingClientRect();
        const logicalWidth = Math.max(1, Math.round(rect.width));
        const logicalHeight = Math.max(1, Math.round(rect.height));
        const cssWidth = rect.width;
        const cssHeight = rect.height;
        const pixelWidth = Math.max(1, Math.round(cssWidth * dpr));
        const pixelHeight = Math.max(1, Math.round(cssHeight * dpr));

        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight)
        {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        if (logicalWidth !== lastLogicalWidth ||
            logicalHeight !== lastLogicalHeight ||
            pixelWidth !== lastPixelWidth ||
            pixelHeight !== lastPixelHeight) {
            lastLogicalWidth = logicalWidth;
            lastLogicalHeight = logicalHeight;
            lastPixelWidth = pixelWidth;
            lastPixelHeight = pixelHeight;
            exports.Vellum.Web.Application.Resize(logicalWidth, logicalHeight, pixelWidth, pixelHeight);
        }
    }

    function pointerToCanvasLogical(clientX, clientY) {
        const rect = canvas.getBoundingClientRect();
        return {
            x: clientX - rect.left,
            y: clientY - rect.top
        };
    }

    function updatePointer(evt, wheelY = 0) {
        const point = pointerToCanvasLogical(evt.clientX, evt.clientY);
        exports.Vellum.Web.Application.SetPointerState(point.x, point.y, evt.buttons ?? 0, wheelY);
    }

    function mainLoop(timeMs) {
        resizeCanvasToDisplaySize();
        exports.Vellum.Web.Application.UpdateFrame(timeMs);
        window.requestAnimationFrame(mainLoop);
    }

    window.addEventListener('resize', resizeCanvasToDisplaySize);
    window.addEventListener('orientationchange', resizeCanvasToDisplaySize);
    canvas.addEventListener('pointermove', evt => updatePointer(evt));
    canvas.addEventListener('pointerdown', evt => {
        canvas.setPointerCapture?.(evt.pointerId);
        updatePointer(evt);
        evt.preventDefault();
    }, { passive: false });
    canvas.addEventListener('pointerup', evt => updatePointer(evt));
    canvas.addEventListener('pointercancel', evt => updatePointer(evt));
    canvas.addEventListener('pointerleave', evt => {
        const point = pointerToCanvasLogical(evt.clientX, evt.clientY);
        exports.Vellum.Web.Application.SetPointerState(point.x, point.y, 0, 0);
    });
    canvas.addEventListener('wheel', evt => {
        updatePointer(evt, -evt.deltaY / 100);
        evt.preventDefault();
    }, { passive: false });

    await runMain();
    resizeCanvasToDisplaySize();

    document.getElementById('spinner')?.remove();
    window.requestAnimationFrame(mainLoop);
}

initialize().catch(err => {
    console.error('An error occurred during initialization:', err);
});
