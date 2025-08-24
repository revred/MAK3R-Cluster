// Debug particles.js loading
console.log('particles-debug.js loaded');

function debugParticles() {
    console.log('=== PARTICLES DEBUG ===');
    console.log('Window width:', window.innerWidth);
    console.log('jQuery available:', typeof $ !== 'undefined');
    console.log('particlesJS available:', typeof particlesJS !== 'undefined');
    console.log('particles-js element exists:', document.getElementById('particles-js') !== null);
    
    if (document.getElementById('particles-js')) {
        const el = document.getElementById('particles-js');
        console.log('particles-js element dimensions:', el.offsetWidth, 'x', el.offsetHeight);
        const styles = window.getComputedStyle(el);
        console.log('particles-js position:', styles.position);
        console.log('particles-js display:', styles.display);
        console.log('particles-js visibility:', styles.visibility);
        console.log('particles-js z-index:', styles.zIndex);
    }
    
    // Check if particles are already initialized
    if (window.pJSDom && window.pJSDom.length > 0) {
        console.log('Particles already initialized:', window.pJSDom.length, 'instances');
        return;
    }
    
    // Force initialize particles for testing - simplified version
    if (typeof particlesJS !== 'undefined' && document.getElementById('particles-js')) {
        console.log('Force initializing particles with simple config...');
        
        try {
            particlesJS('particles-js', {
                particles: {
                    number: { value: 80, density: { enable: true, value_area: 800 } },
                    color: { value: '#3DA8FF' },
                    shape: { type: 'circle' },
                    opacity: { value: 0.8, random: false },
                    size: { value: 3, random: true },
                    line_linked: {
                        enable: true,
                        distance: 150,
                        color: '#3DA8FF',
                        opacity: 0.4,
                        width: 1
                    },
                    move: {
                        enable: true,
                        speed: 6,
                        direction: 'none',
                        random: false,
                        straight: false,
                        out_mode: 'out',
                        bounce: false
                    }
                },
                interactivity: {
                    detect_on: 'canvas',
                    events: {
                        onhover: { enable: true, mode: 'grab' },
                        onclick: { enable: false },
                        resize: true
                    },
                    modes: {
                        grab: { distance: 140, line_linked: { opacity: 1 } }
                    }
                },
                retina_detect: true
            });
            
            console.log('Particles initialization completed');
            console.log('pJSDom instances:', window.pJSDom ? window.pJSDom.length : 'none');
            
            // Check if canvas was created
            setTimeout(() => {
                const canvas = document.querySelector('#particles-js canvas');
                console.log('Canvas created:', canvas !== null);
                if (canvas) {
                    console.log('Canvas dimensions:', canvas.width, 'x', canvas.height);
                }
            }, 500);
            
        } catch (error) {
            console.error('Particles initialization failed:', error);
        }
    } else {
        console.log('Cannot initialize particles - missing dependencies or element');
        console.log('particlesJS type:', typeof particlesJS);
        console.log('Element found:', !!document.getElementById('particles-js'));
    }
}

// Try multiple times with delays
console.log('Setting up particle initialization...');
document.addEventListener('DOMContentLoaded', debugParticles);
window.addEventListener('load', debugParticles);
setTimeout(debugParticles, 500);
setTimeout(debugParticles, 1000);
setTimeout(debugParticles, 2000);