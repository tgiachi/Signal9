import { Calendar, dateFnsLocalizer, Views } from 'react-big-calendar';
import { format, parse, startOfWeek, getDay } from 'date-fns';
import { it } from 'date-fns/locale/it';
import 'react-big-calendar/lib/css/react-big-calendar.css';
import type { ScheduleBlock } from './schedule-types';

const locales = { 'it-IT': it };
const localizer = dateFnsLocalizer({ format, parse, startOfWeek, getDay, locales });

const RULE_COLOR: Record<string, string> = {
  Pin: '#dd6b20',
  Series: '#3182ce',
  TagPool: '#38a169',
};

// react-big-calendar wants concrete dates. Project the weekly block onto the
// "current viewing week" — pick the Monday of the rendered range.
function projectOnto(referenceWeekStart: Date, block: ScheduleBlock) {
  const [h, m] = block.startTime.split(':').map(Number);
  const dayOffset = (block.dayOfWeek + 6) % 7; // shift so Monday=0
  const start = new Date(referenceWeekStart);
  start.setDate(referenceWeekStart.getDate() + dayOffset);
  start.setHours(h ?? 0, m ?? 0, 0, 0);
  const end = new Date(start.getTime() + block.durationMinutes * 60_000);
  return { start, end };
}

type CalEvent = {
  id: string;
  title: string;
  start: Date;
  end: Date;
  resource: ScheduleBlock;
};

type Props = {
  blocks: ScheduleBlock[];
  onSelectSlot: (info: { start: Date; end: Date }) => void;
  onSelectEvent: (block: ScheduleBlock) => void;
};

export function ScheduleBlockGrid({ blocks, onSelectSlot, onSelectEvent }: Props) {
  const refWeekStart = startOfWeek(new Date(), { weekStartsOn: 1 });
  const events: CalEvent[] = blocks.map((b) => {
    const { start, end } = projectOnto(refWeekStart, b);
    return { id: b.id, title: b.name, start, end, resource: b };
  });

  return (
    <div className="h-[640px] rounded-md bg-bg-2 p-2">
      <Calendar<CalEvent>
        localizer={localizer}
        events={events}
        defaultView={Views.WORK_WEEK}
        views={[Views.WORK_WEEK, Views.WEEK, Views.DAY]}
        selectable
        step={30}
        timeslots={2}
        onSelectSlot={(s) => onSelectSlot({ start: s.start as Date, end: s.end as Date })}
        onSelectEvent={(ev) => onSelectEvent(ev.resource)}
        eventPropGetter={(ev) => {
          const block = ev.resource;
          return { style: { backgroundColor: RULE_COLOR[block.ruleType] ?? '#4a5568' } };
        }}
        toolbar
      />
    </div>
  );
}
