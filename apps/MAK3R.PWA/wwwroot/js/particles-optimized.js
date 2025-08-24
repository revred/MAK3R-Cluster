// Ultra-optimized particles with mouse interaction for MAK3R.ai
class OptimizedParticles {
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);
        if (!this.container) return;

        // Performance-optimized defaults
        this.options = {
            particleCount: window.innerWidth > 1200 ? 80 : 60,
            maxDistance: 150,
            particleSpeed: 0.1,
            particleSize: 1.5,
            lineWidth: 1,
            color: '#3DA8FF',
            opacity: 0.7,
            mouseRadius: 150,
            ...options
        };

        this.particles = [];
        this.mouse = { x: null, y: null };
        this.animationId = null;
        this.lastTime = 0;
        this.targetFPS = 60;
        this.frameInterval = 1000 / this.targetFPS;

        this.init();
    }

    init() {
        this.createCanvas();
        this.createParticles();
        this.bindEvents();
        this.animate();
    }

    createCanvas() {
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'particles-canvas';
        this.canvas.style.cssText = `
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            pointer-events: none;
            z-index: 1;
        `;
        this.container.appendChild(this.canvas);
        this.ctx = this.canvas.getContext('2d', { alpha: true });
        
        this.resize();
    }

    resize() {
        const rect = this.container.getBoundingClientRect();
        const dpr = Math.min(window.devicePixelRatio || 1, 2); // Cap at 2x for performance
        
        this.canvas.width = rect.width * dpr;
        this.canvas.height = rect.height * dpr;
        this.canvas.style.width = rect.width + 'px';
        this.canvas.style.height = rect.height + 'px';
        
        this.ctx.scale(dpr, dpr);
        this.width = rect.width;
        this.height = rect.height;
    }

    createParticles() {
        this.particles = [];
        for (let i = 0; i < this.options.particleCount; i++) {
            this.particles.push({
                x: Math.random() * this.width,
                y: Math.random() * this.height,
                vx: (Math.random() - 0.5) * this.options.particleSpeed,
                vy: (Math.random() - 0.5) * this.options.particleSpeed,
                originalVx: (Math.random() - 0.5) * this.options.particleSpeed,
                originalVy: (Math.random() - 0.5) * this.options.particleSpeed,
                size: this.options.particleSize + Math.random() * 0.5
            });
        }
    }

    bindEvents() {
        // Real-time mouse tracking for interaction
        const updateMouse = (e) => {
            const rect = this.container.getBoundingClientRect();
            this.mouse.x = e.clientX - rect.left;
            this.mouse.y = e.clientY - rect.top;
        };

        // Use document for global mouse tracking
        document.addEventListener('mousemove', updateMouse);

        document.addEventListener('mouseleave', () => {
            this.mouse.x = null;
            this.mouse.y = null;
        });

        // Optimized resize handler
        let resizeTimeout;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(() => {
                this.resize();
                this.createParticles();
            }, 150);
        });
    }

    updateParticles() {
        for (let i = 0; i < this.particles.length; i++) {
            const p = this.particles[i];
            
            // Simple constant movement - always moving
            p.x += p.originalVx;
            p.y += p.originalVy;

            // Boundary wrapping
            if (p.x < 0) p.x = this.width;
            if (p.x > this.width) p.x = 0;
            if (p.y < 0) p.y = this.height;
            if (p.y > this.height) p.y = 0;
        }
    }

    drawParticles() {
        const { ctx, options } = this;
        
        // Clear canvas
        ctx.clearRect(0, 0, this.width, this.height);
        
        // Draw connections first (behind particles)
        ctx.strokeStyle = `rgba(61, 168, 255, ${options.opacity * 0.3})`;
        ctx.lineWidth = options.lineWidth;
        ctx.beginPath();

        // Optimized connection drawing with distance pre-filtering
        for (let i = 0; i < this.particles.length; i++) {
            const p1 = this.particles[i];
            
            // Mouse connection
            if (this.mouse.x !== null) {
                const dx = this.mouse.x - p1.x;
                const dy = this.mouse.y - p1.y;
                const distance = Math.sqrt(dx * dx + dy * dy);
                
                if (distance < this.options.mouseRadius) {
                    const opacity = (1 - distance / this.options.mouseRadius) * options.opacity;
                    ctx.strokeStyle = `rgba(61, 168, 255, ${opacity})`;
                    ctx.moveTo(p1.x, p1.y);
                    ctx.lineTo(this.mouse.x, this.mouse.y);
                }
            }
            
            // Particle connections (only check forward to avoid duplicates)
            for (let j = i + 1; j < this.particles.length; j++) {
                const p2 = this.particles[j];
                const dx = p1.x - p2.x;
                const dy = p1.y - p2.y;
                const distance = dx * dx + dy * dy; // Skip sqrt for performance
                
                if (distance < options.maxDistance * options.maxDistance) {
                    const realDistance = Math.sqrt(distance);
                    const opacity = (1 - realDistance / options.maxDistance) * options.opacity * 0.5;
                    ctx.strokeStyle = `rgba(61, 168, 255, ${opacity})`;
                    ctx.moveTo(p1.x, p1.y);
                    ctx.lineTo(p2.x, p2.y);
                }
            }
        }
        ctx.stroke();

        // Draw particles
        ctx.fillStyle = `rgba(61, 168, 255, ${options.opacity})`;
        for (let i = 0; i < this.particles.length; i++) {
            const p = this.particles[i];
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    animate(currentTime = 0) {
        // Remove FPS limiting - let it run at full speed
        this.updateParticles();
        this.drawParticles();
        
        this.animationId = requestAnimationFrame((time) => this.animate(time));
    }

    destroy() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
        }
        if (this.canvas) {
            this.canvas.remove();
        }
    }
}

// Auto-initialize with performance optimizations
document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('particles-js')) {
        // Only run on decent hardware to avoid performance issues
        const isGoodHardware = window.navigator.hardwareConcurrency > 2 && 
                              window.innerWidth > 768;
        
        window.particlesInstance = new OptimizedParticles('particles-js', {
            particleCount: window.innerWidth > 1400 ? 100 : 80,
            maxDistance: 150,
            particleSpeed: 3.0,
            mouseRadius: 180,
            opacity: 0.8
        });
        console.log('High-speed particles initialized with mouse interaction');
    }
});

// Performance monitoring
document.addEventListener('visibilitychange', () => {
    if (window.particlesInstance) {
        if (document.hidden) {
            // Reduce particle count when tab is not visible
            window.particlesInstance.options.particleCount = Math.floor(
                window.particlesInstance.options.particleCount * 0.3
            );
        } else {
            // Restore when tab becomes visible
            window.particlesInstance.createParticles();
        }
    }
});