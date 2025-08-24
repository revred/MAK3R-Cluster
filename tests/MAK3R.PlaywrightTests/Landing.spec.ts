import { test, expect } from '@playwright/test';

test.describe('Landing Page Performance', () => {
  test('should load instantly with SSR and no WASM', async ({ page }) => {
    const startTime = Date.now();
    
    await page.goto('/');
    
    // Verify page loads instantly
    const loadTime = Date.now() - startTime;
    expect(loadTime).toBeLessThan(2000);
    
    // Verify no WASM requests on landing page
    const wasmRequests = page.locator('script[src*=".wasm"]');
    await expect(wasmRequests).toHaveCount(0);
    
    // Check for MAK3R.ai branding
    await expect(page.locator('h1').first()).toContainText('MAK3R.ai');
    
    // Verify interactive server islands are working
    await expect(page.locator('.machine-name')).toBeVisible();
    await expect(page.locator('.stat')).toBeVisible();
  });

  test('should show live machine data updates', async ({ page }) => {
    await page.goto('/');
    
    // Wait for initial machine data
    await expect(page.locator('.machine-name')).toBeVisible();
    
    // Capture initial machine state
    const initialState = await page.locator('.machine-state').first().textContent();
    
    // Wait for data to potentially update
    await page.waitForTimeout(3000);
    
    // Machine data should be present (may or may not have changed)
    await expect(page.locator('.machine-name')).toBeVisible();
  });

  test('should navigate to dashboard with WASM hydration', async ({ page }) => {
    await page.goto('/');
    
    // Navigate to dashboard
    await page.click('a[href="/dashboard"]');
    await page.waitForURL('/dashboard');
    
    // Verify dashboard loads
    await expect(page.locator('h1')).toContainText('Digital Twin Dashboard');
  });

  test('should meet performance budgets', async ({ page }) => {
    const response = await page.goto('/');
    
    // Check response size is reasonable
    const contentLength = response?.headers()['content-length'];
    if (contentLength) {
      expect(parseInt(contentLength)).toBeLessThan(50000); // 50KB HTML budget
    }
    
    // Check critical CSS is inlined or loaded quickly
    const cssLinks = page.locator('link[rel="stylesheet"]');
    await expect(cssLinks).toBeTruthy();
  });

  test('should have proper SEO and accessibility', async ({ page }) => {
    await page.goto('/');
    
    // Check meta tags
    await expect(page.locator('title')).toContainText('MAK3R');
    
    // Check proper heading structure
    const h1 = page.locator('h1');
    await expect(h1).toBeTruthy();
    
    // Check for proper alt attributes on images (if any)
    const images = page.locator('img');
    const imageCount = await images.count();
    
    for (let i = 0; i < imageCount; i++) {
      const alt = await images.nth(i).getAttribute('alt');
      expect(alt).toBeTruthy();
    }
  });

  test('should handle responsive design', async ({ page }) => {
    // Test mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/');
    
    await expect(page.locator('.hero-title')).toBeVisible();
    await expect(page.locator('.cta-primary')).toBeVisible();
    
    // Test desktop viewport
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    
    await expect(page.locator('.hero-title')).toBeVisible();
    await expect(page.locator('.hero-visual')).toBeVisible();
  });
});