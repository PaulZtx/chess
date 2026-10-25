import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement>

const baseProps: IconProps = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.8,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
}

export function UploadIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5" />
      <path d="M5 14.5v4A1.5 1.5 0 006.5 20h11a1.5 1.5 0 001.5-1.5v-4" />
    </svg>
  )
}

export function ArrowIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="M5 12h14m-5-5 5 5-5 5" />
    </svg>
  )
}

export function ChevronIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="m8 10 4 4 4-4" />
    </svg>
  )
}

export function SparkIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="M12 3c.5 4.9 3.1 7.5 8 8-4.9.5-7.5 3.1-8 8-.5-4.9-3.1-7.5-8-8 4.9-.5 7.5-3.1 8-8Z" />
    </svg>
  )
}

export function BackIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="M19 12H5m5 5-5-5 5-5" />
    </svg>
  )
}

export function PlayIcon(props: IconProps) {
  return (
    <svg {...baseProps} {...props}>
      <path d="m9 7 8 5-8 5V7Z" />
    </svg>
  )
}
