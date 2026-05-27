declare module 'vanta/dist/vanta.net.min' {
  import type * as THREE from 'three';

  export type VantaNetOptions = {
    el: HTMLElement;
    THREE: typeof THREE;
    backgroundAlpha?: number;
    backgroundColor?: number;
    color?: number;
    gyroControls?: boolean;
    maxDistance?: number;
    minHeight?: number;
    minWidth?: number;
    mouseControls?: boolean;
    points?: number;
    scale?: number;
    scaleMobile?: number;
    showDots?: boolean;
    spacing?: number;
    touchControls?: boolean;
  };

  export type VantaEffect = {
    destroy: () => void;
  };

  export default function NET(options: VantaNetOptions): VantaEffect;
}
