// Optimized particles.js configuration for MAK3R.ai landing page
function initParticles() {
    console.log('Initializing particles...');
    if (typeof particlesJS !== 'undefined' && document.getElementById('particles-js')) {
        console.log('particlesJS found, initializing...');
        // Always run for testing, remove width check temporarily
        particlesJS('particles-js', {
                particles: {
                    number: {
                        value: 60, // Reduced from 100 for better performance
                        density: { enable: true, value_area: 1200 }
                    },
                    color: { 
                        value: '#3DA8FF' // MAK3R accent color
                    },
                    shape: {
                        type: 'circle',
                        stroke: { width: 0, color: '#000000' }
                    },
                    opacity: {
                        value: 0.6, // Reduced opacity for subtlety
                        random: true,
                        anim: {
                            enable: true,
                            speed: 1,
                            opacity_min: 0.2,
                            sync: false
                        }
                    },
                    size: {
                        value: 2, // Smaller particles
                        random: true,
                        anim: {
                            enable: false,
                            speed: 4,
                            size_min: 0.3,
                            sync: false
                        }
                    },
                    line_linked: {
                        enable: true,
                        distance: 120, // Reduced connection distance
                        color: '#3DA8FF',
                        opacity: 0.3, // Very subtle lines
                        width: 1
                    },
                    move: {
                        enable: true,
                        speed: 1.5, // Slower, more subtle movement
                        direction: 'none',
                        random: true,
                        straight: false,
                        out_mode: 'out',
                        bounce: false,
                        attract: { enable: false }
                    }
                },
                interactivity: {
                    detect_on: 'canvas',
                    events: {
                        onhover: { 
                            enable: true, 
                            mode: 'grab' 
                        },
                        onclick: { 
                            enable: false // Disabled for landing page
                        },
                        resize: true
                    },
                    modes: {
                        grab: { 
                            distance: 200, 
                            line_linked: { opacity: 0.6 }
                        }
                    }
                },
                retina_detect: true
            });
        console.log('Particles initialized successfully');
    } else {
        console.log('particlesJS not found or particles-js element missing');
    }
}

// Try multiple initialization methods
document.addEventListener('DOMContentLoaded', initParticles);
window.addEventListener('load', initParticles);
// Fallback timeout
setTimeout(initParticles, 1000);

// Pause particles when page is not visible for performance
document.addEventListener('visibilitychange', function() {
    if (window.pJSDom && window.pJSDom[0] && window.pJSDom[0].pJS) {
        const pJS = window.pJSDom[0].pJS;
        if (document.hidden) {
            if (pJS.fn.drawAnimFrame) {
                cancelAnimationFrame(pJS.fn.drawAnimFrame);
                pJS.fn.drawAnimFrame = null;
            }
        } else {
            if (!pJS.fn.drawAnimFrame && pJS.particles.move.enable) {
                pJS.fn.vendors.draw();
            }
        }
    }
});

// Clean up when leaving the page
window.addEventListener('beforeunload', function() {
    if (window.pJSDom && window.pJSDom[0] && window.pJSDom[0].pJS) {
        if (window.pJSDom[0].pJS.fn.drawAnimFrame) {
            cancelAnimationFrame(window.pJSDom[0].pJS.fn.drawAnimFrame);
        }
    }
});