<template>
  <div class="plate-field">
    <label v-if="label" :for="inputId">{{ label }}</label>
    <input
      :id="inputId"
      v-bind="$attrs"
      :value="plateDisplay"
      type="text"
      inputmode="text"
      enterkeyhint="done"
      :maxlength="PLATE_DISPLAY_MAX_LENGTH"
      autocapitalize="characters"
      autocomplete="off"
      spellcheck="false"
      :aria-label="ariaLabelComputed"
      :placeholder="placeholder"
      :autofocus="autofocus"
      @input="onInput"
      @blur="onBlur"
      @keydown.enter="onKeydownEnter"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import {
  PLATE_DISPLAY_MAX_LENGTH,
  formatPlateDisplay,
  plateDisplayIndexToRawLength,
  plateRawLengthToDisplayIndex,
  sanitizePlateInput,
} from '@/lib/plate'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    modelValue: string
    label?: string
    id?: string
    ariaLabel?: string
    placeholder?: string
    autofocus?: boolean
  }>(),
  {
    label: '',
    id: '',
    ariaLabel: '',
    placeholder: 'ABC-1D23',
    autofocus: false,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string]
  submit: []
}>()

const plateRaw = ref(sanitizePlateInput(props.modelValue))

watch(
  () => props.modelValue,
  (v) => {
    plateRaw.value = sanitizePlateInput(v ?? '')
  },
)

const plateDisplay = computed(() => formatPlateDisplay(plateRaw.value))

const inputId = computed(() => props.id || 'plate-field')
const ariaLabelComputed = computed(() => props.ariaLabel || props.label || 'Placa do veículo')

function syncParent(): void {
  emit('update:modelValue', plateRaw.value)
}

function onInput(e: Event): void {
  const el = e.target as HTMLInputElement
  const start = el.selectionStart ?? 0
  const end = el.selectionEnd ?? 0
  const beforeDisp = formatPlateDisplay(plateRaw.value)
  const rawCursor = plateDisplayIndexToRawLength(start, el.value)
  plateRaw.value = sanitizePlateInput(el.value)
  const afterRaw = plateRaw.value
  syncParent()
  nextTick(() => {
    let pos: number
    if (start === end && start >= beforeDisp.length) {
      pos = formatPlateDisplay(afterRaw).length
    } else {
      const clampedRaw = Math.min(rawCursor, afterRaw.length)
      pos = plateRawLengthToDisplayIndex(clampedRaw)
    }
    el.setSelectionRange(pos, pos)
  })
}

function onBlur(): void {
  plateRaw.value = sanitizePlateInput(plateRaw.value)
  syncParent()
}

function onKeydownEnter(e: KeyboardEvent): void {
  if (e.key !== 'Enter') return
  e.preventDefault()
  emit('submit')
}
</script>

<style scoped>
.plate-field label {
  display: block;
  margin-bottom: 0.25rem;
}

.plate-field input {
  width: 100%;
  max-width: 12rem;
  font-family: ui-monospace, monospace;
  letter-spacing: 0.02em;
}
</style>
