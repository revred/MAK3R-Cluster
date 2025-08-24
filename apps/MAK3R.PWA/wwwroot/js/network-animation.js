class NetworkAnimation {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d');
        this.nodes = [];
        this.edges = [];
        this.dataPoints = [];
        this.animationId = null;
        this.initialized = false;
        
        // Performance optimizations
        this.offscreenCanvas = document.createElement('canvas');
        this.offscreenCtx = this.offscreenCanvas.getContext('2d');
        
        // Animation settings
        this.config = {
            nodeCount: 25,
            maxDistance: 120,
            nodeSpeed: 0.3,
            pulseSpeed: 0.02,
            dataPointCount: 8,
            dataPointSpeed: 1.5,
            colors: {
                node: 'rgba(61, 168, 255, 0.6)',
                edge: 'rgba(61, 168, 255, 0.1)',
                dataPoint: 'rgba(61, 168, 255, 0.8)',
                nodeCore: 'rgba(61, 168, 255, 0.9)'
            }
        };
        
        this.init();
    }
    
    init() {
        if (this.initialized) return;
        
        this.resize();
        this.createNodes();
        this.createDataPoints();
        
        // Event listeners
        window.addEventListener('resize', () => this.resize());
        
        // Start animation with RAF
        this.animate();
        this.initialized = true;
    }
    
    resize() {
        const rect = this.canvas.parentElement.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        
        this.canvas.width = rect.width * dpr;
        this.canvas.height = rect.height * dpr;
        this.canvas.style.width = rect.width + 'px';
        this.canvas.style.height = rect.height + 'px';
        
        this.offscreenCanvas.width = this.canvas.width;
        this.offscreenCanvas.height = this.canvas.height;
        
        this.ctx.scale(dpr, dpr);
        this.offscreenCtx.scale(dpr, dpr);
        
        this.width = rect.width;
        this.height = rect.height;
    }
    
    createNodes() {
        this.nodes = [];
        for (let i = 0; i < this.config.nodeCount; i++) {
            this.nodes.push({
                x: Math.random() * this.width,
                y: Math.random() * this.height,
                vx: (Math.random() - 0.5) * this.config.nodeSpeed,
                vy: (Math.random() - 0.5) * this.config.nodeSpeed,
                pulse: Math.random() * Math.PI * 2,
                size: 2 + Math.random() * 3
            });
        }
    }
    
    createDataPoints() {
        this.dataPoints = [];
        for (let i = 0; i < this.config.dataPointCount; i++) {
            this.dataPoints.push({
                x: Math.random() * this.width,
                y: Math.random() * this.height,
                targetX: Math.random() * this.width,
                targetY: Math.random() * this.height,
                speed: 0.5 + Math.random() * this.config.dataPointSpeed,
                size: 1 + Math.random() * 2,
                life: Math.random()
            });
        }
    }
    
    updateNodes() {
        this.nodes.forEach(node => {
            // Update position
            node.x += node.vx;
            node.y += node.vy;
            
            // Boundary collision with gentle bounce
            if (node.x <= 0 || node.x >= this.width) node.vx *= -0.8;
            if (node.y <= 0 || node.y >= this.height) node.vy *= -0.8;
            
            // Keep in bounds
            node.x = Math.max(0, Math.min(this.width, node.x));
            node.y = Math.max(0, Math.min(this.height, node.y));
            
            // Update pulse
            node.pulse += this.config.pulseSpeed;
        });
    }
    
    updateDataPoints() {
        this.dataPoints.forEach(point => {
            // Move towards target
            const dx = point.targetX - point.x;
            const dy = point.targetY - point.y;
            const distance = Math.sqrt(dx * dx + dy * dy);
            
            if (distance > 5) {
                point.x += (dx / distance) * point.speed;
                point.y += (dy / distance) * point.speed;
            } else {
                // Set new target
                point.targetX = Math.random() * this.width;
                point.targetY = Math.random() * this.height;
            }
            
            // Update life cycle
            point.life += 0.01;
            if (point.life > 1) point.life = 0;
        });
    }
    
    drawEdges() {
        this.ctx.strokeStyle = this.config.colors.edge;
        this.ctx.lineWidth = 0.5;
        
        // Use quadratic optimization - only check nearby nodes
        for (let i = 0; i < this.nodes.length; i++) {
            for (let j = i + 1; j < this.nodes.length; j++) {
                const dx = this.nodes[i].x - this.nodes[j].x;
                const dy = this.nodes[i].y - this.nodes[j].y;
                const distance = Math.sqrt(dx * dx + dy * dy);
                
                if (distance < this.config.maxDistance) {
                    const opacity = (1 - distance / this.config.maxDistance) * 0.3;
                    this.ctx.globalAlpha = opacity;
                    
                    this.ctx.beginPath();
                    this.ctx.moveTo(this.nodes[i].x, this.nodes[i].y);
                    this.ctx.lineTo(this.nodes[j].x, this.nodes[j].y);
                    this.ctx.stroke();
                }
            }
        }
        this.ctx.globalAlpha = 1;
    }
    
    drawNodes() {
        this.nodes.forEach(node => {
            const pulseSize = node.size + Math.sin(node.pulse) * 0.5;
            
            // Core node
            this.ctx.fillStyle = this.config.colors.nodeCore;
            this.ctx.beginPath();
            this.ctx.arc(node.x, node.y, pulseSize * 0.6, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Outer glow
            const gradient = this.ctx.createRadialGradient(
                node.x, node.y, 0,
                node.x, node.y, pulseSize * 2
            );
            gradient.addColorStop(0, this.config.colors.node);
            gradient.addColorStop(1, 'rgba(61, 168, 255, 0)');
            
            this.ctx.fillStyle = gradient;
            this.ctx.beginPath();
            this.ctx.arc(node.x, node.y, pulseSize * 2, 0, Math.PI * 2);
            this.ctx.fill();
        });
    }
    
    drawDataPoints() {
        this.dataPoints.forEach(point => {
            const alpha = Math.sin(point.life * Math.PI) * 0.8;
            this.ctx.globalAlpha = alpha;
            
            this.ctx.fillStyle = this.config.colors.dataPoint;
            this.ctx.beginPath();
            this.ctx.arc(point.x, point.y, point.size, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Data point trail
            this.ctx.strokeStyle = this.config.colors.dataPoint;
            this.ctx.lineWidth = 1;
            this.ctx.globalAlpha = alpha * 0.3;
            
            this.ctx.beginPath();
            this.ctx.moveTo(point.x - point.speed * 3, point.y - point.speed * 3);
            this.ctx.lineTo(point.x, point.y);
            this.ctx.stroke();
        });
        this.ctx.globalAlpha = 1;
    }
    
    render() {
        // Clear canvas
        this.ctx.clearRect(0, 0, this.width, this.height);
        
        // Draw components in order
        this.drawEdges();
        this.drawNodes();
        this.drawDataPoints();
    }
    
    animate() {
        this.updateNodes();
        this.updateDataPoints();
        this.render();
        
        this.animationId = requestAnimationFrame(() => this.animate());
    }
    
    destroy() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
        window.removeEventListener('resize', this.resize);
        this.initialized = false;
    }
}

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    const canvas = document.getElementById('network-animation');
    if (canvas) {
        window.networkAnimation = new NetworkAnimation('network-animation');
    }
});

// Pause animation when page is hidden for performance
document.addEventListener('visibilitychange', function() {
    if (window.networkAnimation) {
        if (document.hidden) {
            if (window.networkAnimation.animationId) {
                cancelAnimationFrame(window.networkAnimation.animationId);
                window.networkAnimation.animationId = null;
            }
        } else {
            if (!window.networkAnimation.animationId) {
                window.networkAnimation.animate();
            }
        }
    }
});