import { Component, OnInit, computed, signal } from '@angular/core';
import { CalendarConnectionsClient, CalendarConnectionDto, CalendarEventDto, EventsClient } from '../web-api-client';

const HOUR_HEIGHT_PX = 48;
const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

interface PositionedEvent {
  event: CalendarEventDto;
  top: number;
  height: number;
  left: number;
  width: number;
}

function startOfDay(date: Date): Date {
  const result = new Date(date);
  result.setHours(0, 0, 0, 0);
  return result;
}

function startOfWeek(date: Date): Date {
  const result = startOfDay(date);
  result.setDate(result.getDate() - result.getDay());
  return result;
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date);
  result.setDate(result.getDate() + days);
  return result;
}

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

@Component({
  standalone: false,
  selector: 'app-calendar',
  templateUrl: './calendar.component.html',
  styleUrls: ['./calendar.component.scss']
})
export class CalendarComponent implements OnInit {
  readonly hourHeightPx = HOUR_HEIGHT_PX;
  readonly hours = Array.from({ length: 24 }, (_, i) => i);
  readonly gridHeightPx = 24 * HOUR_HEIGHT_PX;

  weekStart = signal(startOfWeek(new Date()));
  events = signal<CalendarEventDto[] | null>(null);
  connections = signal<CalendarConnectionDto[]>([]);
  loading = signal(false);

  days = computed(() => Array.from({ length: 7 }, (_, i) => addDays(this.weekStart(), i)));

  rangeLabel = computed(() => {
    const start = this.weekStart();
    const end = addDays(start, 6);
    const sameMonth = start.getMonth() === end.getMonth() && start.getFullYear() === end.getFullYear();
    if (sameMonth) {
      return `${MONTH_NAMES[start.getMonth()]} ${start.getDate()}–${end.getDate()}, ${end.getFullYear()}`;
    }
    return `${MONTH_NAMES[start.getMonth()]} ${start.getDate()} – ${MONTH_NAMES[end.getMonth()]} ${end.getDate()}, ${end.getFullYear()}`;
  });

  allDayEventsByDay = computed(() => {
    const events = this.events() ?? [];
    return this.days().map(day => events.filter(e => e.isAllDay && isSameDay(new Date(e.startUtc!), day)));
  });

  timedEventsByDay = computed(() => {
    const events = (this.events() ?? []).filter(e => !e.isAllDay);
    return this.days().map(day => this.layoutDay(events.filter(e => isSameDay(new Date(e.startUtc!), day)), day));
  });

  constructor(private eventsClient: EventsClient, private connectionsClient: CalendarConnectionsClient) {}

  ngOnInit(): void {
    this.connectionsClient.getCalendarConnections().subscribe({
      next: vm => this.connections.set((vm.connections ?? []).filter(c => c.isVisible)),
      error: error => console.error(error)
    });

    this.loadEvents();
  }

  private loadEvents(): void {
    this.loading.set(true);
    const start = this.weekStart();
    const end = addDays(start, 7);

    this.eventsClient.getMergedEvents(start, end).subscribe({
      next: events => {
        this.events.set(events);
        this.loading.set(false);
      },
      error: error => {
        console.error(error);
        this.loading.set(false);
      }
    });
  }

  previousWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), -7));
    this.loadEvents();
  }

  nextWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), 7));
    this.loadEvents();
  }

  goToToday(): void {
    this.weekStart.set(startOfWeek(new Date()));
    this.loadEvents();
  }

  dayLabel(day: Date): string {
    return DAY_NAMES[day.getDay()];
  }

  isToday(day: Date): boolean {
    return isSameDay(day, new Date());
  }

  hourLabel(hour: number): string {
    if (hour === 0) return '12 AM';
    if (hour === 12) return '12 PM';
    return hour < 12 ? `${hour} AM` : `${hour - 12} PM`;
  }

  private layoutDay(events: CalendarEventDto[], day: Date): PositionedEvent[] {
    const dayStart = startOfDay(day).getTime();
    const dayEnd = dayStart + 24 * 60 * 60 * 1000;

    const withMinutes = events
      .map(event => {
        const startMs = Math.max(new Date(event.startUtc!).getTime(), dayStart);
        const endMs = Math.min(Math.max(new Date(event.endUtc!).getTime(), startMs + 15 * 60 * 1000), dayEnd);
        return { event, startMin: (startMs - dayStart) / 60000, endMin: (endMs - dayStart) / 60000 };
      })
      .sort((a, b) => a.startMin - b.startMin || a.endMin - b.endMin);

    // Greedy column assignment for overlapping events, grouped into clusters that share columns.
    const columnEnds: number[] = [];
    const assigned: { item: typeof withMinutes[number]; column: number }[] = [];
    let clusterStart: { item: typeof withMinutes[number]; column: number }[] = [];
    let clusterMaxEnd = -Infinity;
    const clusters: { items: { item: typeof withMinutes[number]; column: number }[]; columns: number }[] = [];

    for (const item of withMinutes) {
      if (item.startMin >= clusterMaxEnd && clusterStart.length > 0) {
        clusters.push({ items: clusterStart, columns: columnEnds.length });
        clusterStart = [];
        columnEnds.length = 0;
        clusterMaxEnd = -Infinity;
      }

      let column = columnEnds.findIndex(end => end <= item.startMin);
      if (column === -1) {
        column = columnEnds.length;
        columnEnds.push(item.endMin);
      } else {
        columnEnds[column] = item.endMin;
      }

      const entry = { item, column };
      assigned.push(entry);
      clusterStart.push(entry);
      clusterMaxEnd = Math.max(clusterMaxEnd, item.endMin);
    }

    if (clusterStart.length > 0) {
      clusters.push({ items: clusterStart, columns: columnEnds.length });
    }

    const columnsByEvent = new Map<{ item: typeof withMinutes[number]; column: number }, number>();
    for (const cluster of clusters) {
      for (const entry of cluster.items) {
        columnsByEvent.set(entry, cluster.columns);
      }
    }

    return assigned.map(entry => {
      const columns = columnsByEvent.get(entry) ?? 1;
      const width = 100 / columns;
      return {
        event: entry.item.event,
        top: (entry.item.startMin / 60) * HOUR_HEIGHT_PX,
        height: Math.max(((entry.item.endMin - entry.item.startMin) / 60) * HOUR_HEIGHT_PX, 18),
        left: entry.column * width,
        width
      };
    });
  }
}
