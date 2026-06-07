// Particles.js initialization helper
window.initializeParticles = function () {
    return new Promise((resolve, reject) => {
        try {
            // Check if particlesJS is loaded
            if (typeof particlesJS === 'undefined') {
                console.error('particlesJS library not loaded');
                reject('particlesJS library not loaded');
                return;
            }

            // Load particles configuration
            particlesJS.load('particles-js', '/particles.json', function () {
                console.log('Particles.js initialized successfully');
                resolve(true);
            });
        } catch (error) {
            console.error('Error initializing particles.js:', error);
            reject(error);
        }
    });
};

// Cleanup function
window.destroyParticles = function () {
    try {
        if (window.pJSDom && window.pJSDom.length > 0) {
            window.pJSDom[0].pJS.fn.vendors.destroypJS();
            window.pJSDom = [];
            console.log('Particles.js destroyed');
        }
    } catch (error) {
        console.error('Error destroying particles.js:', error);
    }
};
