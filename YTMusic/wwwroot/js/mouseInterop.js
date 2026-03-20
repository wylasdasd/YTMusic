window.mouseDragInterop = {
    startDrag: function (dotNetObjRef) {
        function onMouseMove() {
            dotNetObjRef.invokeMethodAsync('OnGlobalMouseMove');
        }

        function onMouseUp() {
            dotNetObjRef.invokeMethodAsync('OnGlobalMouseUp');
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
        }

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    }
};
