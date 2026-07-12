<!--
  Override of VitePress's default VPHomeContent. The stock component sets an
  inline `--vp-offset` style computed from the client window width, which is
  empty during SSR and non-empty after hydration — a benign but noisy Vue
  "hydration style mismatch" on the home page. That variable only drives the
  full-width breakout of .VPHomeSponsors / .VPTeamPage (unused here), and the
  CSS already falls back to `calc(50% - 50vw)`, so dropping the inline style is
  safe and removes the mismatch. Wired via a Vite alias in config.mts.
-->
<template>
  <div class="vp-doc container">
    <slot />
  </div>
</template>

<style scoped>
.container {
  margin: auto;
  width: 100%;
  max-width: 1280px;
  padding: 0 24px;
}

@media (min-width: 640px) {
  .container {
    padding: 0 48px;
  }
}

@media (min-width: 960px) {
  .container {
    width: 100%;
    padding: 0 64px;
  }
}

.vp-doc :deep(.VPHomeSponsors),
.vp-doc :deep(.VPTeamPage) {
  margin-left: var(--vp-offset, calc(50% - 50vw));
  margin-right: var(--vp-offset, calc(50% - 50vw));
}

.vp-doc :deep(.VPHomeSponsors h2) {
  border-top: none;
  letter-spacing: normal;
}

.vp-doc :deep(.VPHomeSponsors a),
.vp-doc :deep(.VPTeamPage a) {
  text-decoration: none;
}
</style>
