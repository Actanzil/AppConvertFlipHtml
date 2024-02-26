document.addEventListener('DOMContentLoaded', function() {
    // Inisialisasi library data-aos
    AOS.init();
});

$(document).ready(function () {
    $('#conversionForm').submit(function () {
        $('#spinner').show();
        $('#buttonText').text('Mengonversi...');
    });
});



