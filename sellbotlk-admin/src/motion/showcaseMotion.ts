import { useLayoutEffect } from 'react'
import { countUp, gsap, motionMode, revealHeading, ScrollTrigger } from './core'
import { motionConfig as C } from './motionConfig'

/** Architecture / API / Security: H1 line reveal on load, sub fade, card/row rise-ins, count-ups. */
function initShowcase(root: HTMLElement): () => void {
  const ctx = gsap.context(() => {
    const title = root.querySelector<HTMLElement>('.sc-title')
    if (title) revealHeading(title, { delay: 0.1 })
    gsap.from(root.querySelectorAll('.sc-hero .eyebrow, .sc-sub'), { opacity: 0, y: 14, duration: C.rise.dur, ease: C.ease.settle, stagger: 0.12, delay: 0.25 })
    for (const group of ['.flow__node', '.pillar', '.api-row', '.num-card', '.list-card']) {
      const items = root.querySelectorAll<HTMLElement>(group)
      if (!items.length) continue
      gsap.from(items, { y: C.rise.y, opacity: 0, duration: C.rise.dur, ease: C.ease.settle, stagger: C.stagger.rows, scrollTrigger: { trigger: items[0], start: C.rise.start, once: true } })
    }
    root.querySelectorAll<HTMLElement>('.num-card [data-count]').forEach((n, i) => countUp(n, { trigger: n, delay: i * C.stagger.statRows }))
  }, root)
  void document.fonts?.ready.then(() => ScrollTrigger.refresh())
  return () => ctx.revert()
}

/** Call from a presentation page; runs once per mount, reverted on unmount. */
export function useShowcaseMotion() {
  useLayoutEffect(() => {
    const root = document.querySelector<HTMLElement>('.sc-main')
    if (!root || motionMode() !== 'full') return
    return initShowcase(root)
  }, [])
}
