<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Bar } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale
} from 'chart.js'

ChartJS.register(Title, Tooltip, Legend, BarElement, CategoryScale, LinearScale)

// Use VitePress font family
ChartJS.defaults.font.family = 'Inter, ui-sans-serif, system-ui, sans-serif'

const props = defineProps<{
  title?: string
  labels: string[]
  datasets: {
    label: string
    data: number[]
    backgroundColor?: string
  }[]
  horizontal?: boolean
  yMax?: number
  yLabel?: string
}>()

const isMounted = ref(false)
onMounted(() => {
  isMounted.value = true
})

const chartData = {
  labels: props.labels,
  datasets: props.datasets.map((ds, i) => ({
    ...ds,
    backgroundColor: ds.backgroundColor || ['#22c55e', '#3b82f6', '#f59e0b', '#ef4444'][i % 4]
  }))
}

const chartOptions = {
  indexAxis: props.horizontal ? 'y' as const : 'x' as const,
  responsive: true,
  maintainAspectRatio: true,
  plugins: {
    legend: {
      position: 'top' as const,
      labels: {
        color: '#d1d5db'
      }
    },
    title: {
      display: !!props.title,
      text: props.title,
      color: '#f3f4f6',
      font: {
        size: 16
      }
    }
  },
  scales: {
    x: {
      ticks: { color: '#9ca3af' },
      grid: { color: '#374151' },
      title: props.horizontal ? {
        display: !!props.yLabel,
        text: props.yLabel,
        color: '#9ca3af'
      } : {}
    },
    y: {
      max: props.horizontal ? undefined : props.yMax,
      ticks: { color: '#9ca3af' },
      grid: { color: '#374151' },
      title: props.horizontal ? {} : {
        display: !!props.yLabel,
        text: props.yLabel,
        color: '#9ca3af'
      }
    }
  }
}
</script>

<template>
  <div class="chart-container" style="background: #1f2937; border-radius: 8px; padding: 16px; margin: 16px 0;">
    <Bar v-if="isMounted" :data="chartData" :options="chartOptions" />
    <div v-else style="height: 300px; display: flex; align-items: center; justify-content: center; color: #9ca3af;">
      Loading chart...
    </div>
  </div>
</template>
