<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  src: { type: String, required: true },
  alt: { type: String, default: '' },
  duration: { type: Number, default: 10 },
  restartDelay: { type: Number, default: 5 },
  maxWidth: { type: String, default: '750px' }
})

const wrapper = ref(null)
const container = ref(null)
const isPlaying = ref(false)

let svgContent = ''
let observer = null
let restartTimer = null

async function fetchSvg() {
  try {
    const resp = await fetch(props.src)
    if (!resp.ok) return
    svgContent = await resp.text()
    if (container.value) container.value.innerHTML = svgContent
  } catch (e) {
    console.error('AnimatedSvg: failed to load', props.src, e)
  }
}

function play() {
  isPlaying.value = true
  scheduleRestart()
}

function scheduleRestart() {
  clearTimeout(restartTimer)
  restartTimer = setTimeout(replay, (props.duration + props.restartDelay) * 1000)
}

function replay() {
  clearTimeout(restartTimer)
  const el = container.value
  if (!el || !svgContent) return
  // Remove and re-insert SVG to reset all CSS animations
  el.innerHTML = ''
  void el.offsetHeight
  el.innerHTML = svgContent
  isPlaying.value = true
  scheduleRestart()
}

onMounted(async () => {
  await fetchSvg()

  observer = new IntersectionObserver(([entry]) => {
    if (!isPlaying.value && entry.isIntersecting) {
      // "fully visible" = ratio ≥ 0.95, OR element covers most of viewport
      const fullyVisible = entry.intersectionRatio >= 0.95
      const coversViewport = entry.rootBounds
        && entry.boundingClientRect.height >= entry.rootBounds.height
        && entry.intersectionRect.height >= entry.rootBounds.height * 0.85
      if (fullyVisible || coversViewport) play()
    }
  }, { threshold: Array.from({ length: 21 }, (_, i) => i / 20) })

  if (wrapper.value) observer.observe(wrapper.value)
})

onUnmounted(() => {
  clearTimeout(restartTimer)
  observer?.disconnect()
})
</script>

<template>
  <div ref="wrapper" class="animated-svg" :style="{ maxWidth, width: '100%', margin: '1rem 0' }">
    <div
      ref="container"
      :class="['animated-svg-inner', { playing: isPlaying }]"
      role="img"
      :aria-label="alt"
    />
    <button class="animated-svg-replay" @click="replay" title="Replay animation" aria-label="Replay animation">
      <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none"
           stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <path d="M1 4v6h6"/><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"/>
      </svg>
    </button>
  </div>
</template>

<style scoped>
.animated-svg {
  position: relative;
}

/* Pause all animations until .playing is set */
.animated-svg-inner :deep(*) {
  animation-play-state: paused !important;
}
.animated-svg-inner.playing :deep(*) {
  animation-play-state: running !important;
}

.animated-svg-replay {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  border: 1px solid rgba(0, 0, 0, 0.1);
  background: rgba(255, 255, 255, 0.7);
  color: #666;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0.45;
  transition: opacity 0.2s, background 0.2s;
  z-index: 1;
  padding: 0;
}
.animated-svg-replay:hover {
  opacity: 1;
  background: rgba(255, 255, 255, 0.95);
}
</style>
