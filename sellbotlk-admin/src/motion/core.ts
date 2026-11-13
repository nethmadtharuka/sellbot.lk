import gsap from 'gsap'
import { ScrollTrigger } from 'gsap/ScrollTrigger'
import Lenis from 'lenis'
import { useEffect } from 'react'
import { motionConfig as C } from './motionConfig'

gsap.registerPlugin(ScrollTrigger)
export { gsap, ScrollTrigger }

export type MotionMode = 'off' | 'reduced' | 'full'

/** One flag decides everything: config switch, ?motion=off, then prefers-reduced-motion. */
export function motionMode(): MotionMode {
  if (!C.enabled || typeof window === 'undefined') return 'off'
  if (new URLSearchParams(window.location.search).get('motion') === 'off') return 'off'
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'reduced' : 'full'
}

/**
 * Lenis smooth scroll synced to GSAP's ticker so ScrollTrigger reads the smoothed position.
 * Touch devices keep native scrolling (Lenis default: syncTouch off). Off in reduced/off modes.
 */
export function useSmoothScroll(active: boolean) {
  useEffect(() => {
    if (!active || motionMode() !== 'full') return
    const lenis = new Lenis({ duration: C.smoothScroll.duration, wheelMultiplier: C.smoothScroll.wheelMultiplier })
    lenis.on('scroll', ScrollTrigger.update)
    const tick = (t: number) => lenis.raf(t * 1000)
    gsap.ticker.add(tick)
    gsap.ticker.lagSmoothing(0)
    // Re-measure after fonts and images settle, and (debounced) after resizes.
    let timer = 0
    const refresh = () => { window.clearTimeout(timer); timer = window.setTimeout(() => ScrollTrigger.refresh(), 200) }
    void document.fonts?.ready.then(refresh)
    window.addEventListener('load', refresh)
    window.addEventListener('resize', refresh)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('load', refresh)
      window.removeEventListener('resize', refresh)
      gsap.ticker.remove(tick)
      lenis.destroy()
    }
  }, [active])
}

/**
 * Wraps each word of `el` in a mask (.mw) with an inner span (.mw__i) that can rise from 100 %.
 * Keeps inline elements (<em>, <br>) intact, so styling and line breaks are unchanged. Splitting
 * by words only means Sinhala/Tamil clusters are never cut. Idempotent.
 */
export function splitWords(el: HTMLElement): HTMLElement[] {
  if (!el.dataset.split) {
    const walk = (node: Node) => {
      for (const child of [...node.childNodes]) {
        if (child.nodeType === Node.TEXT_NODE) {
          const parts = (child.textContent ?? '').split(/(\s+)/)
          const frag = document.createDocumentFragment()
          for (const p of parts) {
            if (!p) continue
            if (/^\s+$/.test(p)) { frag.appendChild(document.createTextNode(p)); continue }
            const mask = document.createElement('span'); mask.className = 'mw'
            const inner = document.createElement('span'); inner.className = 'mw__i'; inner.textContent = p
            mask.appendChild(inner); frag.appendChild(mask)
          }
          child.replaceWith(frag)
        } else if (child.nodeType === Node.ELEMENT_NODE && (child as Element).tagName !== 'BR') walk(child)
      }
    }
    walk(el)
    el.dataset.split = '1'
  }
  return [...el.querySelectorAll<HTMLElement>('.mw__i')]
}

/** Line index of each word (by rendered top), so words on one line move together. */
export function lineIndex(words: HTMLElement[]): number[] {
  const tops: number[] = []
  return words.map((w) => {
    const top = Math.round((w.parentElement ?? w).getBoundingClientRect().top)
    let i = tops.findIndex((t) => Math.abs(t - top) < 4)
    if (i < 0) { tops.push(top); i = tops.length - 1 }
    return i
  })
}

/** Line-mask reveal: each line rises from 100 % inside its mask, lines staggered. */
export function revealHeading(el: HTMLElement, opts: { delay?: number; scroll?: boolean } = {}) {
  const words = splitWords(el)
  const lines = lineIndex(words)
  gsap.set(words, { yPercent: 105 })
  return gsap.to(words, {
    yPercent: 0, duration: C.reveal.dur, ease: C.ease.settle, delay: opts.delay ?? 0,
    stagger: (i) => lines[i] * C.stagger.lines,
    scrollTrigger: opts.scroll ? { trigger: el, start: C.reveal.start, once: true } : undefined,
  })
}

/**
 * Count-up: the DOM keeps the final value (and a screen-reader copy); only the visible
 * aria-hidden span shows the rolling integer. Decelerates (settle ease).
 */
export function countUp(el: HTMLElement, opts: { delay?: number; trigger?: Element } = {}) {
  const roll = el.querySelector<HTMLElement>('.count__roll') ?? el
  const target = Number(el.dataset.count)
  if (!Number.isFinite(target)) return
  const state = { v: 0 }
  roll.textContent = '0'
  return gsap.to(state, {
    v: target, duration: C.count.dur, ease: C.ease.settle, delay: opts.delay ?? 0,
    onUpdate: () => { roll.textContent = String(Math.round(state.v)) },
    onComplete: () => { roll.textContent = String(target) },
    scrollTrigger: opts.trigger ? { trigger: opts.trigger, start: C.count.start, once: true } : undefined,
  })
}

/** Scroll-scrubbed parallax: speed < 1 lags, > 1 runs ahead. Uses `y` only. */
export function parallax(el: Element, speed: number, factor = 1) {
  if (speed === 1) return
  return gsap.to(el, {
    y: () => (1 - speed) * factor * (window.innerHeight + (el as HTMLElement).offsetHeight) * 0.5,
    ease: C.ease.scrub,
    scrollTrigger: { trigger: el, start: 'top bottom', end: 'bottom top', scrub: C.scrubLag, invalidateOnRefresh: true },
  })
}
