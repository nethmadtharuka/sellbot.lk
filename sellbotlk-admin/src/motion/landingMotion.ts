/**
 * Landing page motion. Pure layer on top of the existing markup: it only reads elements by class,
 * sets transforms/opacity, and reverts everything on cleanup (gsap.context + matchMedia).
 *
 * Trigger types (kept separate):
 *  - load-timed:      hero intro (3.1)
 *  - scroll-scrubbed: frame expansion (3.5), parallax (3.1), sheen (3.2), how convergence (3.3),
 *                     sheet transitions (3.8), heading exit (3.9 step 4), masonry speeds (3.12)
 *  - scroll-once:     heading reveals (3.7), ticker + icon (3.9), counts (3.10), tile rise, pill drop (3.13)
 *  - loops:           ticker rotation (3.9), ring pulse (3.3; CSS), both paused out of view
 */
import { countUp, gsap, lineIndex, parallax, revealHeading, ScrollTrigger, splitWords, type MotionMode } from './core'
import { motionConfig as C } from './motionConfig'

export function initLanding(root: HTMLElement, mode: MotionMode): () => void {
  if (mode === 'off') return () => undefined
  if (mode === 'reduced') {
    root.classList.add('is-motion-reduced') // static ticker word + icon; everything else as designed
    return () => root.classList.remove('is-motion-reduced')
  }
  root.classList.add('is-motion')

  const q = <T extends Element = HTMLElement>(s: string) => root.querySelector<T & HTMLElement>(s)
  const qa = (s: string) => [...root.querySelectorAll<HTMLElement>(s)]
  const vh = () => window.innerHeight
  let tickerLoop: gsap.core.Timeline | null = null

  const ctx = gsap.context(() => {
    const frame = q('.hero-frame'), frameInner = q('.hero-frame__inner'), hero = q('.hero')
    const arc = q('.hero__arc'), nav = q('.hero-nav'), navPill = q('.hero-nav__pill'), copy = q('.hero__copy')
    const title = q('.hero__title'), sub = q('.hero__sub'), buttons = qa('.hero__btn')
    const guard = q('.float-chip--guard'), lang = q('.float-chip--lang'), tests = q('.float-chip--tests')
    const wordmark = q('.wordmark'), how = q('.how'), howGroup = q('.how__cards'), howCards = qa('.how-card')
    const pick = q('.pick'), pickHead = q('.pick__head'), tiles = qa('.tile'), built = q('.built')
    if (!frame || !hero || !how || !pick || !built) return

    // Centred elements keep their CSS translateX(-50%) as GSAP xPercent so y can be added safely.
    gsap.set([arc, wordmark], { xPercent: -50, x: 0 })

    /* ---------------- 3.1 hero intro (load-timed, all breakpoints) ---------------- */
    const I = C.intro
    const titleWords = title ? splitWords(title) : []
    const titleLines = lineIndex(titleWords)
    const intro = gsap.timeline({ defaults: { ease: C.ease.settle } })
    intro
      .from(arc, { opacity: 0, duration: I.glow.dur, ease: C.ease.inout }, I.glow.at)
      .from(nav, { opacity: 0, y: -12, duration: I.nav.dur }, I.nav.at)
      .fromTo(titleWords, { yPercent: 105 }, { yPercent: 0, duration: I.title.dur, stagger: (i) => titleLines[i] * C.stagger.lines }, I.title.at)
      .from(sub, { opacity: 0, y: 16, duration: I.sub.dur }, I.sub.at)
      .from(buttons, { opacity: 0, y: 24, duration: I.buttons.dur, stagger: I.buttons.stagger }, I.buttons.at)
      .from(guard, { opacity: 0, x: I.chips.distance, duration: I.chips.dur }, I.chips.at)
      .from(lang, { opacity: 0, x: -I.chips.distance, duration: I.chips.dur }, I.chips.at + I.chips.gap)
      .from(tests, { opacity: 0, yPercent: 60, duration: I.chips.dur }, I.chips.at + I.chips.gap * 2)
      .from(qa('.float-chip__tag, .float-chip__conf'), { opacity: 0, scale: 0.4, duration: I.tags.dur, ease: C.ease.pop, stagger: C.stagger.rows }, I.tags.at)
      .from(q('.ring'), { scale: 0, duration: I.accentPop.dur, ease: C.ease.pop }, I.accentPop.at)
    for (const n of qa('.float-chip--tests [data-count]')) countUp(n, { delay: I.chips.at + I.chips.gap * 2 })

    /* ---------------- 3.7 heading reveals (scroll-once) ---------------- */
    for (const h of [q('.how__title'), q('.pick__title')]) if (h) revealHeading(h, { scroll: true })

    /* ---------------- 3.10 counts in the tiles ---------------- */
    for (const n of qa('.tile [data-count]')) countUp(n, { trigger: tiles[0] })

    /* ---------------- 3.13 stack pills drop with a bounce ---------------- */
    const pills = qa('.built__chip')
    gsap.from(pills, {
      y: C.pills.from, opacity: 0, rotation: (i) => (i % 2 ? 1 : -1) * C.pills.tilt * (0.5 + ((i * 37) % 10) / 20),
      duration: C.pills.dur, ease: C.ease.bounce, stagger: C.stagger.pills,
      scrollTrigger: { trigger: built, start: C.pills.start, once: true },
    })
    gsap.from(q('.built__note'), { opacity: 0, y: 20, duration: C.rise.dur, ease: C.ease.settle, scrollTrigger: { trigger: built, start: C.pills.start, once: true }, delay: 0.4 })

    /* ---------------- 3.9 ticker pill + icon (scroll-once), loop while in view ---------------- */
    const ticker = q('.ticker'), words = qa('.ticker__word'), icon = q('.pick__icon')
    if (ticker && words.length) {
      const T = C.ticker
      const widthOf = (w: HTMLElement) => w.offsetWidth + 2 * parseFloat(getComputedStyle(ticker).paddingLeft)
      const finalH = ticker.offsetHeight // measured before collapsing to a dot
      gsap.set(ticker, { width: 8, height: 8, scale: 0 })
      gsap.set(words, { yPercent: 110, opacity: 0 })
      gsap.set(icon, { y: T.iconFrom, rotation: T.iconTilt, opacity: 0, scale: 0.6 })
      const enter = gsap.timeline({ paused: true, defaults: { ease: C.ease.settle } })
      enter
        .to(ticker, { scale: 1, duration: T.dotDur, ease: C.ease.pop })
        .to(ticker, { width: () => widthOf(words[0]), height: finalH, duration: T.expandDur })
        .to(words[0], { yPercent: 0, opacity: 1, duration: T.rollDur }, '<0.1')
        .to(icon, { y: 0, rotation: 0, opacity: 1, scale: 1, duration: T.iconDur, ease: C.ease.pop }, T.dotDur + T.iconDelay)
      // Rotation loop: out up through the mask, in from below, pill width tracks the word.
      tickerLoop = gsap.timeline({ paused: true, repeat: -1 })
      words.forEach((w, i) => {
        const next = words[(i + 1) % words.length]
        const at = (i + 1) * T.every
        tickerLoop!
          .to(w, { yPercent: -110, opacity: 0, duration: T.rollDur, ease: C.ease.settle }, at)
          .fromTo(next, { yPercent: 110, opacity: 0 }, { yPercent: 0, opacity: 1, duration: T.rollDur, ease: C.ease.settle, immediateRender: false }, at)
          .to(ticker, { width: () => widthOf(next), duration: T.rollDur, ease: C.ease.settle }, at)
      })
      let entered = false
      ScrollTrigger.create({
        trigger: pickHead ?? pick, start: T.start, end: 'bottom top',
        onEnter: () => { if (!entered) { entered = true; enter.play().then(() => tickerLoop?.play()) } else tickerLoop?.play() },
        onEnterBack: () => { if (entered) tickerLoop?.play() },
        onLeave: () => tickerLoop?.pause(),
        onLeaveBack: () => tickerLoop?.pause(),
      })
    }

    /* ---------------- breakpoint-specific scenes ---------------- */
    const mm = gsap.matchMedia()
    mm.add({ desktop: C.media.desktop, tablet: C.media.tablet, mobile: C.media.mobile, wide: C.media.wide }, (mq) => {
      const { desktop, tablet, mobile, wide } = mq.conditions as Record<string, boolean>
      const big = desktop || tablet
      const pad = parseFloat(getComputedStyle(root).paddingTop) || 0
      const F = C.frame

      /* 3.5 full-bleed expansion (scrubbed, from scroll 0); the tilt/ghost start was removed on request */
      const len = (mobile ? F.mobile.length : F.length)
      // Starts exactly as designed (no tilt, no shrink), then eases out to full width.
      gsap.set(hero, { transformOrigin: '50% 0%', borderRadius: F.radius })
      const exp = gsap.timeline({
        defaults: { ease: C.ease.scrub },
        scrollTrigger: { start: 0, end: () => vh() * len, scrub: C.scrubLag, invalidateOnRefresh: true },
      })
      exp.to(hero, { scale: () => window.innerWidth / hero.offsetWidth, y: -pad, borderRadius: 0, duration: 1 }, 0)
         .to(copy, { y: () => (F.copyExit.y * vh()) / 100, opacity: 0, duration: F.copyExit.to - F.copyExit.from }, F.copyExit.from)

      /* 3.6 nav restyle: state toggle at the expansion threshold (not scrubbed) */
      ScrollTrigger.create({
        start: () => vh() * len * C.nav.lightAt,
        onEnter: () => navPill?.classList.add('is-light'), onLeaveBack: () => navPill?.classList.remove('is-light'),
      })

      /* 3.1 layered parallax after load */
      if (big) {
        const f = tablet ? C.parallax.tablet : 1
        parallax(wordmark!, C.parallax.wordmark, f); parallax(arc!, C.parallax.arc, f)
        parallax(guard!, C.parallax.chipGuard, f); parallax(lang!, C.parallax.chipLang, f); parallax(tests!, C.parallax.chipTests, f)
      }

      /* 3.2 glass sheen drift */
      if (big) gsap.to(q('.sheen'), { y: C.sheen.drift, ease: C.ease.scrub, scrollTrigger: { start: 0, end: 'max', scrub: true } })

      /* 3.8 sheet: how slides over the pinned hero, hero scales back and dims */
      if (big) {
        ScrollTrigger.create({ trigger: frame, start: 'bottom bottom', endTrigger: how, end: 'top top', pin: true, pinSpacing: false })
        // Dims to the sheet opacity, then fades out: how-it-works is shorter than the viewport, so the
        // hero would otherwise show as a strip beneath it once its pin ends.
        gsap.timeline({ defaults: { ease: C.ease.scrub }, scrollTrigger: { trigger: how, start: 'top bottom', end: 'top top', scrub: C.scrubLag } })
          .to(frameInner, { scale: C.sheet.scale, opacity: C.sheet.opacity, duration: 0.7 })
          .to(frameInner, { opacity: 0, duration: 0.3 })
      }

      /* 3.3 how-it-works: cards converge (scrubbed) while pinned, then the group hands off */
      if (big) {
        const H = C.how, pinLen = desktop ? H.pinLength.desktop : H.pinLength.tablet
        const conv = gsap.timeline({ defaults: { ease: C.ease.scrub }, scrollTrigger: { trigger: how, start: 'top 70%', end: () => `+=${vh() * (0.7 + pinLen * 0.45)}` /* composed by mid-pin */, scrub: C.scrubLag, invalidateOnRefresh: true } })
        conv.from(howCards[0], { xPercent: -H.fromX, opacity: 0 }, 0)
            .from(howCards[2], { xPercent: H.fromX, opacity: 0 }, 0)
            .from(howCards[1], { yPercent: H.fromY, opacity: 0 }, 0)
        // pinSpacing must be explicit: ScrollTrigger defaults it to false when the parent (.landing) is flex.
        ScrollTrigger.create({ trigger: how, start: 'top top', end: () => `+=${vh() * pinLen}`, pin: true, pinSpacing: true, invalidateOnRefresh: true })
        gsap.to(howGroup, { scale: H.exitScale, y: () => (H.exitY * vh()) / 100, ease: C.ease.scrub, scrollTrigger: { trigger: pick, start: 'top bottom', end: 'top top', scrub: C.scrubLag, invalidateOnRefresh: true } })
      } else {
        gsap.from(howCards, { y: C.rise.y, opacity: 0, duration: C.rise.dur, ease: C.ease.settle, stagger: C.stagger.tiles, scrollTrigger: { trigger: howGroup, start: C.rise.start, once: true } })
      }
      ScrollTrigger.create({ trigger: how, start: 'top bottom', end: 'bottom top', toggleClass: { targets: q('.how-ring')!, className: 'is-live' } })

      /* 3.9 step 4: heading block exits (scrubbed) while tiles rise (once) */
      gsap.from(tiles, { y: C.ticker.tileRise, opacity: 0, duration: C.rise.dur, ease: C.ease.settle, stagger: C.stagger.tiles, scrollTrigger: { trigger: q('.pick__tiles'), start: C.rise.start, once: true } })
      if (big) gsap.to(pickHead, { y: -60, opacity: 0, ease: C.ease.scrub, scrollTrigger: { trigger: q('.pick__tiles'), start: 'top 40%', end: 'top 0%', scrub: C.scrubLag } })

      /* 3.12 columns at different speeds (yPercent, so it never fights the rise on y) */
      if (wide) tiles.forEach((t, i) => {
        const s = C.masonry[i % C.masonry.length]
        gsap.fromTo(t, { yPercent: (s - 1) * 60 }, { yPercent: (1 - s) * 60, ease: C.ease.scrub, scrollTrigger: { trigger: q('.pick__tiles'), start: 'top bottom', end: 'bottom top', scrub: C.scrubLag } })
      })

      /* 3.8 sheet: built slides over the pinned pick */
      if (big) {
        ScrollTrigger.create({ trigger: pick, start: 'bottom bottom', endTrigger: built, end: 'top top', pin: true, pinSpacing: false })
        gsap.to(pick, { scale: C.sheet.scale, opacity: C.sheet.opacity, ease: C.ease.scrub, scrollTrigger: { trigger: built, start: 'top bottom', end: 'top top', scrub: C.scrubLag } })
      }
    })
  }, root)

  // Re-measure once fonts have loaded (line indices, word widths, pin lengths).
  void document.fonts?.ready.then(() => ScrollTrigger.refresh())

  return () => {
    tickerLoop?.kill()
    ctx.revert()
    root.classList.remove('is-motion')
  }
}
