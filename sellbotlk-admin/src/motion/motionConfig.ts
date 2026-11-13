/**
 * Every motion value in one place. Values marked (est.) were estimated from reference-video
 * frames (relative timing and ease shape, not exact curves); tune them here.
 * Set `enabled: false` (or open any page with ?motion=off) to remove all motion: the site then
 * renders exactly as designed, fully visible, with native scrolling.
 */
export const motionConfig = {
  enabled: true,

  /** Breakpoints (gsap.matchMedia). Desktop = full behaviour; tablet = reduced; mobile = minimal. */
  media: {
    desktop: '(min-width: 1280px)',
    tablet: '(min-width: 768px) and (max-width: 1279px)',
    mobile: '(max-width: 767px)',
    wide: '(min-width: 1024px)', // masonry column speeds only
  },

  /** Lenis smooth scroll (est.). Touch keeps native scrolling. */
  smoothScroll: { duration: 1.2, wheelMultiplier: 1 },

  /** Named eases. Scrubbed motion is always linear ('none'). */
  ease: {
    settle: 'expo.out', // text reveals, rise-ins, slide-ins: fast start, long soft landing
    pop: 'back.out(1.7)', // ~10 % overshoot: tags, ring, icon landing
    bounce: 'bounce.out', // physical bounce for dropping pills
    scrub: 'none',
    inout: 'power2.inOut', // cross-fades, nav restyle
  },
  scrubLag: 0.8, // seconds of smoothing on scrubbed timelines (0.6-1)

  stagger: { rows: 0.075, words: 0.05, lines: 0.08, tiles: 0.08, pills: 0.09, statRows: 0.15 },

  /** Parallax multipliers (1 = normal scroll). Tablet uses half the offset; mobile none. */
  parallax: { wordmark: 0.88, arc: 0.92, chipGuard: 1.15, chipLang: 1.2, chipTests: 1.1, tablet: 0.5 },

  /** 3.1 hero intro, seconds from load (est.) */
  intro: {
    glow: { at: 0, dur: 0.4 },
    nav: { at: 0.05, dur: 0.6 },
    title: { at: 0.2, dur: 0.9 },
    sub: { at: 0.45, dur: 0.8 },
    buttons: { at: 0.6, dur: 0.8, stagger: 0.1 },
    chips: { at: 0.7, dur: 0.9, gap: 0.12, distance: 120 },
    tags: { at: 1.05, dur: 0.5 },
    accentPop: { at: 1.4, dur: 0.6 },
  },

  /** 3.2 glass sheen overlay */
  sheen: { opacity: 0.06, drift: 120 /* px over the whole page; 0 = static */ },

  /** 3.3 how-it-works convergence (pinned) */
  how: {
    pinLength: { desktop: 1.0, tablet: 0.6 }, // x viewport height
    fromX: 110, fromY: 60, // % of card size
    exitScale: 0.85, exitY: -8, // vh
    ringLoop: 2.4, // s, scale 1 -> 1.08, opacity .5 -> 1
  },

  /** 3.5 hero full-bleed expansion (scrubbed over `length` x vh). Starts at the designed size, no tilt. */
  frame: {
    radius: 40, length: 1.0,
    copyExit: { from: 0.15, to: 0.6, y: -15 /* vh */ },
    mobile: { length: 0.6 },
  },

  /** 3.6 nav restyle threshold (progress of the frame expansion) */
  nav: { lightAt: 0.35, dur: 0.25 },

  /** 3.7 headline reveal */
  reveal: { dur: 0.9, start: 'top 85%' },

  /** 3.8 sheet transitions */
  sheet: { scale: 0.95, opacity: 0.8 },

  /** 3.9 ticker + icon */
  ticker: { start: 'top 60%', dotDur: 0.25, expandDur: 0.35, every: 1.8, rollDur: 0.45, iconDelay: 0.1, iconDur: 0.6, iconFrom: -24, iconTilt: -15, tileRise: 80 },

  /** 3.10 count-ups */
  count: { dur: 1.0, start: 'top 75%' },

  /** 3.12 masonry column speeds (>= 1024 px) */
  masonry: [0.9, 1.1, 0.95],

  /** 3.13 dropping pills */
  pills: { from: -48, tilt: 6, dur: 0.7, start: 'top 80%' },

  /** Generic once-only rise-in used for cards/rows */
  rise: { y: 40, dur: 0.9, start: 'top 85%' },

  /** Reduced-motion fallback */
  reduced: { fade: 0.15 },
} as const
