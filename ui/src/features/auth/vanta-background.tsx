import { useEffect, useRef } from 'react';
import type { VantaEffect } from 'vanta/dist/vanta.net.min';
import { resolveVantaNetFactory } from './vanta-loader';

export function VantaBackground() {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return undefined;

    const reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    if (reducedMotion) return undefined;
    if (!window.WebGLRenderingContext) return undefined;

    let destroyed = false;
    let effect: VantaEffect | null = null;

    const initialize = async () => {
      try {
        const [vantaModule, THREE] = await Promise.all([
          import('vanta/dist/vanta.net.min'),
          import('three'),
        ]);
        const NET = resolveVantaNetFactory(vantaModule);

        if (destroyed || !NET) return;

        effect = NET({
          el: container,
          THREE,
          backgroundAlpha: 0,
          backgroundColor: 0x1e272e,
          color: 0x00b894,
          gyroControls: false,
          maxDistance: 22,
          minHeight: 200,
          minWidth: 200,
          mouseControls: true,
          points: 9,
          scale: 1,
          scaleMobile: 0.85,
          showDots: false,
          spacing: 18,
          touchControls: true,
        });
      } catch {
        // Vanta is decorative; the CSS background remains the fallback.
      }
    };

    void initialize();

    return () => {
      destroyed = true;
      effect?.destroy();
    };
  }, []);

  return (
    <div
      ref={containerRef}
      aria-hidden="true"
      data-testid="login-vanta-background"
      className="absolute inset-0 bg-bg-0"
    />
  );
}
