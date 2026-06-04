document.addEventListener('DOMContentLoaded', function() {
    const alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 4000);
    });
});

document.querySelectorAll('input[type="date"]').forEach(input => {
    if (!input.value) {
        const today = new Date().toISOString().split('T')[0];
        input.value = today;
    }
});
