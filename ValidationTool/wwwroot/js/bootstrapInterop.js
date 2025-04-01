window.bootstrapInterop = {
    showModal: function (selector) {
        var modalElement = document.querySelector(selector);
        if (modalElement) {
            var modal = new bootstrap.Modal(modalElement);
            modal.show();
        } else {
            console.error("Modal not found:", selector);
        }
    }
};