// Manufacturing Synergy Animation inspired by mak3r.ai
class ManufacturingSynergy {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        if (!this.container) return;
        
        this.icons = [
            // Outer circle - Technology capabilities
            { id: 'cloud', icon: 'cloud', text: 'Cloud Service', angle: 0, radius: 180, speed: 0.5 },
            { id: 'data', icon: 'sensors', text: 'Shop Floor Data', angle: 30, radius: 180, speed: 0.5 },
            { id: 'security', icon: 'security', text: 'Security & Encryption', angle: 60, radius: 180, speed: 0.5 },
            { id: 'automation', icon: 'smart_toy', text: 'Automation', angle: 90, radius: 180, speed: 0.5 },
            { id: 'container', icon: 'inventory_2', text: 'Containerization', angle: 120, radius: 180, speed: 0.5 },
            { id: 'iot', icon: 'router', text: 'IoT', angle: 150, radius: 180, speed: 0.5 },
            { id: 'track', icon: 'track_changes', text: 'Digital Track & Trace', angle: 180, radius: 180, speed: 0.5 },
            { id: 'graph', icon: 'account_tree', text: 'Graph Database', angle: 210, radius: 180, speed: 0.5 },
            { id: 'scale', icon: 'trending_up', text: 'Scalability', angle: 240, radius: 180, speed: 0.5 },
            { id: 'connect', icon: 'hub', text: 'Connectivity', angle: 270, radius: 180, speed: 0.5 },
            { id: 'collab', icon: 'groups', text: 'Collaboration', angle: 300, radius: 180, speed: 0.5 },
            { id: 'lake', icon: 'storage', text: 'Data Lake', angle: 330, radius: 180, speed: 0.5 },
            
            // Middle circle - Manufacturing processes
            { id: 'special', icon: 'precision_manufacturing', text: 'Special Processes', angle: 45, radius: 120, speed: -0.3 },
            { id: 'additive', icon: 'view_in_ar', text: 'Additive Manufacturing', angle: 135, radius: 120, speed: -0.3 },
            { id: 'subtractive', icon: 'build', text: 'Subtractive Manufacturing', angle: 225, radius: 120, speed: -0.3 },
            { id: 'treatment', icon: 'science', text: 'Treatments', angle: 315, radius: 120, speed: -0.3 }
        ];
        
        this.animationTime = 0;
        this.animationId = null;
        this.init();
    }
    
    init() {
        this.createSVG();
        this.createElements();
        this.animate();
    }
    
    createSVG() {
        this.svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        this.svg.setAttribute('width', '400');
        this.svg.setAttribute('height', '400');
        this.svg.setAttribute('viewBox', '0 0 400 400');
        this.svg.style.cssText = `
            width: 100%;
            height: 100%;
            max-width: 400px;
            max-height: 400px;
        `;
        
        // Create central manufacturing icon
        this.createCentralIcon();
        
        // Create connection lines group
        this.linesGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        this.linesGroup.setAttribute('class', 'connection-lines');
        this.svg.appendChild(this.linesGroup);
        
        // Create icons group
        this.iconsGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        this.iconsGroup.setAttribute('class', 'rotating-icons');
        this.svg.appendChild(this.iconsGroup);
        
        this.container.appendChild(this.svg);
    }
    
    createCentralIcon() {
        const centerGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        centerGroup.setAttribute('transform', 'translate(200, 200)');
        
        // Central circle background
        const centerCircle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        centerCircle.setAttribute('r', '40');
        centerCircle.setAttribute('fill', 'rgba(61, 168, 255, 0.2)');
        centerCircle.setAttribute('stroke', '#3DA8FF');
        centerCircle.setAttribute('stroke-width', '2');
        centerGroup.appendChild(centerCircle);
        
        // Central factory icon
        const centerIcon = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        centerIcon.textContent = '🏭';
        centerIcon.setAttribute('font-size', '32');
        centerIcon.setAttribute('text-anchor', 'middle');
        centerIcon.setAttribute('dominant-baseline', 'middle');
        centerGroup.appendChild(centerIcon);
        
        this.svg.appendChild(centerGroup);
    }
    
    createElements() {
        this.icons.forEach(iconData => {
            this.createIcon(iconData);
        });
    }
    
    createIcon(iconData) {
        const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        group.setAttribute('class', 'icon-group');
        group.setAttribute('data-id', iconData.id);
        
        // Icon circle background
        const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        circle.setAttribute('r', '20');
        circle.setAttribute('fill', 'rgba(18, 24, 33, 0.9)');
        circle.setAttribute('stroke', '#3DA8FF');
        circle.setAttribute('stroke-width', '1');
        group.appendChild(circle);
        
        // Icon (using emoji as fallback for Material Icons)
        const iconMap = {
            'cloud': '☁️', 'sensors': '📊', 'security': '🔒', 'smart_toy': '🤖',
            'inventory_2': '📦', 'router': '📡', 'track_changes': '🔍', 'account_tree': '🌐',
            'trending_up': '📈', 'hub': '🔗', 'groups': '👥', 'storage': '💾',
            'precision_manufacturing': '⚙️', 'view_in_ar': '🖨️', 'build': '🔧', 'science': '🧪'
        };
        
        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        icon.textContent = iconMap[iconData.icon] || '⚡';
        icon.setAttribute('font-size', '16');
        icon.setAttribute('text-anchor', 'middle');
        icon.setAttribute('dominant-baseline', 'middle');
        group.appendChild(icon);
        
        // Tooltip text (initially hidden)
        const tooltip = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        tooltip.textContent = iconData.text;
        tooltip.setAttribute('font-size', '10');
        tooltip.setAttribute('font-family', 'Inter, sans-serif');
        tooltip.setAttribute('fill', '#E6EDF3');
        tooltip.setAttribute('text-anchor', 'middle');
        tooltip.setAttribute('y', '35');
        tooltip.setAttribute('opacity', '0');
        tooltip.setAttribute('class', 'tooltip-text');
        group.appendChild(tooltip);
        
        // Add hover effects
        group.addEventListener('mouseenter', () => {
            tooltip.setAttribute('opacity', '1');
            circle.setAttribute('fill', 'rgba(61, 168, 255, 0.3)');
        });
        
        group.addEventListener('mouseleave', () => {
            tooltip.setAttribute('opacity', '0');
            circle.setAttribute('fill', 'rgba(18, 24, 33, 0.9)');
        });
        
        this.iconsGroup.appendChild(group);
        iconData.element = group;
    }
    
    updatePositions() {
        this.animationTime += 0.01;
        
        // Clear existing connection lines
        this.linesGroup.innerHTML = '';
        
        this.icons.forEach(iconData => {
            // Calculate position
            const currentAngle = iconData.angle + (this.animationTime * iconData.speed * 180 / Math.PI);
            const x = 200 + Math.cos(currentAngle * Math.PI / 180) * iconData.radius;
            const y = 200 + Math.sin(currentAngle * Math.PI / 180) * iconData.radius;
            
            // Update icon position
            iconData.element.setAttribute('transform', `translate(${x}, ${y})`);
            
            // Create connection line to center (subtle)
            if (Math.random() < 0.3) { // Only show some connections
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', '200');
                line.setAttribute('y1', '200');
                line.setAttribute('x2', x);
                line.setAttribute('y2', y);
                line.setAttribute('stroke', '#3DA8FF');
                line.setAttribute('stroke-width', '0.5');
                line.setAttribute('opacity', '0.3');
                this.linesGroup.appendChild(line);
            }
        });
    }
    
    animate() {
        this.updatePositions();
        this.animationId = requestAnimationFrame(() => this.animate());
    }
    
    destroy() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
        }
        if (this.svg) {
            this.svg.remove();
        }
    }
}

// Auto-initialize
document.addEventListener('DOMContentLoaded', () => {
    const container = document.getElementById('manufacturing-synergy');
    if (container) {
        window.manufacturingSynergy = new ManufacturingSynergy('manufacturing-synergy');
        console.log('Manufacturing synergy animation initialized');
    }
});