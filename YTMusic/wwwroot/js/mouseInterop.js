window.mouseDragInterop = {
    startDrag: function (dotNetObjRef) {
        function onMouseMove(e) {
            dotNetObjRef.invokeMethodAsync('OnGlobalMouseMove', e.screenX, e.screenY);
        }

        function onMouseUp(e) {
            dotNetObjRef.invokeMethodAsync('OnGlobalMouseUp', e.screenX, e.screenY);
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
        }

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    }
};
