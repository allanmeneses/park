<template>
  <svg
    class="parking-mark"
    :class="{ 'parking-mark--sm': size === 'sm' }"
    viewBox="0 0 48 48"
    aria-hidden="true"
    focusable="false"
  >
    <title v-if="title">{{ title }}</title>
    <!-- Silhueta + faixa (alinhado ao Android ParkingLogoMark) -->
    <rect x="7" y="7" width="34" height="20" rx="4" fill="currentColor" />
    <rect x="5" y="35" width="38" height="6" rx="2" fill="currentColor" />
  </svg>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    size?: 'md' | 'sm'
    title?: string
  }>(),
  { size: 'md', title: '' },
)
</script>

<style scoped>
.parking-mark {
  display: block;
  width: 3.5rem;
  height: 3.5rem;
  color: var(--color-primary);
}
.parking-mark--sm {
  width: 2rem;
  height: 2rem;
}
</style>
