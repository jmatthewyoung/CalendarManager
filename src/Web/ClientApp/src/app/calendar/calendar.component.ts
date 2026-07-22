import { Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import { CalendarConnectionsClient, CalendarConnectionDto, CalendarEventDto, EventsClient } from '../web-api-client';

const HOUR_HEIGHT_PX = 48;
const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** Default scroll target (8 AM) when a day view has no timed events to scroll to. */
const DEFAULT_DAY_SCROLL_HOUR = 8;

/** How far down the visible viewport the first event of the day should land when day view opens. */
const DAY_SCROLL_TARGET_FRACTION = 0.2;

const TIME_FORMATTER = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' });

interface PositionedEvent {
  event: CalendarEventDto;
  top: number;
  height: number;
  left: number;
  width: number;
}

type ViewMode = 'day' | 'week' | 'month';

interface MonthCell {
  date: Date;
  inCurrentMonth: boolean;
  events: CalendarEventDto[];
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

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date);
  result.setDate(result.getDate() + days);
  return result;
}

function addMonths(date: Date, months: number): Date {
  const result = new Date(date);
  result.setDate(1);
  result.setMonth(result.getMonth() + months);
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
  @ViewChild('eventDialog') eventDialogRef: ElementRef<HTMLDialogElement>;
  @ViewChild('gridBody') gridBodyRef?: ElementRef<HTMLDivElement>;

  readonly hourHeightPx = HOUR_HEIGHT_PX;
  readonly hours = Array.from({ length: 24 }, (_, i) => i);
  readonly gridHeightPx = 24 * HOUR_HEIGHT_PX;
  readonly dayNames = DAY_NAMES;

  viewMode = signal<ViewMode>('week');
  anchorDate = signal(new Date());
  events = signal<CalendarEventDto[] | null>(null);
  connections = signal<CalendarConnectionDto[]>([]);
  loading = signal(false);

  searchQuery = signal('');
  hiddenConnectionIds = signal<ReadonlySet<number>>(new Set());

  selectedEvent = signal<CalendarEventDto | null>(null);

  /** events() narrowed by the in-view search box and any transiently hidden calendars (legend toggles). */
  filteredEvents = computed(() => {
    const events = this.events() ?? [];
    const query = this.searchQuery().trim().toLowerCase();
    const hidden = this.hiddenConnectionIds();

    return events.filter(e =>
      (!query || e.title.toLowerCase().includes(query))
      && !(e.calendarConnectionId != null && hidden.has(e.calendarConnectionId)));
  });

  /** The hour-grid days for day/week view. Month view uses monthWeeks() instead. */
  days = computed(() => {
    if (this.viewMode() === 'day') {
      return [startOfDay(this.anchorDate())];
    }
    const start = startOfWeek(this.anchorDate());
    return Array.from({ length: 7 }, (_, i) => addDays(start, i));
  });

  /** Whether each entry in days() has at least one event, timed or all-day. */
  dayHasEvents = computed(() => {
    const events = this.filteredEvents();
    return this.days().map(day => events.some(e => isSameDay(new Date(e.startUtc!), day)));
  });

  monthWeeks = computed(() => {
    const monthStart = startOfMonth(this.anchorDate());
    const gridStart = startOfWeek(monthStart);
    const events = this.filteredEvents();

    const cells: MonthCell[] = Array.from({ length: 42 }, (_, i) => {
      const date = addDays(gridStart, i);
      return {
        date,
        inCurrentMonth: date.getMonth() === monthStart.getMonth(),
        events: events
          .filter(e => isSameDay(new Date(e.startUtc!), date))
          .sort((a, b) => (a.isAllDay === b.isAllDay ? 0 : a.isAllDay ? -1 : 1)
            || new Date(a.startUtc!).getTime() - new Date(b.startUtc!).getTime())
      };
    });

    return Array.from({ length: 6 }, (_, week) => cells.slice(week * 7, week * 7 + 7));
  });

  rangeLabel = computed(() => {
    if (this.viewMode() === 'day') {
      const day = this.anchorDate();
      return `${DAY_NAMES[day.getDay()]}, ${MONTH_NAMES[day.getMonth()]} ${day.getDate()}, ${day.getFullYear()}`;
    }

    if (this.viewMode() === 'month') {
      const day = this.anchorDate();
      return `${MONTH_NAMES[day.getMonth()]} ${day.getFullYear()}`;
    }

    const start = startOfWeek(this.anchorDate());
    const end = addDays(start, 6);
    const sameMonth = start.getMonth() === end.getMonth() && start.getFullYear() === end.getFullYear();
    if (sameMonth) {
      return `${MONTH_NAMES[start.getMonth()]} ${start.getDate()}–${end.getDate()}, ${end.getFullYear()}`;
    }
    return `${MONTH_NAMES[start.getMonth()]} ${start.getDate()} – ${MONTH_NAMES[end.getMonth()]} ${end.getDate()}, ${end.getFullYear()}`;
  });

  gridTemplateColumns = computed(() => `3.5rem repeat(${this.days().length}, 1fr)`);

  allDayEventsByDay = computed(() => {
    const events = this.filteredEvents();
    return this.days().map(day => events.filter(e => e.isAllDay && isSameDay(new Date(e.startUtc!), day)));
  });

  timedEventsByDay = computed(() => {
    const events = this.filteredEvents().filter(e => !e.isAllDay);
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
    const { start, end } = this.currentRange();

    this.eventsClient.getMergedEvents(start, end).subscribe({
      next: events => {
        this.events.set(events);
        this.loading.set(false);
        this.scrollDayViewToFirstEvent();
      },
      error: error => {
        console.error(error);
        this.loading.set(false);
      }
    });
  }

  private currentRange(): { start: Date; end: Date } {
    switch (this.viewMode()) {
      case 'day': {
        const start = startOfDay(this.anchorDate());
        return { start, end: addDays(start, 1) };
      }
      case 'month': {
        const start = startOfWeek(startOfMonth(this.anchorDate()));
        return { start, end: addDays(start, 42) };
      }
      default: {
        const start = startOfWeek(this.anchorDate());
        return { start, end: addDays(start, 7) };
      }
    }
  }

  setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
    this.loadEvents();
  }

  previous(): void {
    const mode = this.viewMode();
    this.anchorDate.set(
      mode === 'day' ? addDays(this.anchorDate(), -1)
        : mode === 'month' ? addMonths(this.anchorDate(), -1)
        : addDays(this.anchorDate(), -7));
    this.loadEvents();
  }

  next(): void {
    const mode = this.viewMode();
    this.anchorDate.set(
      mode === 'day' ? addDays(this.anchorDate(), 1)
        : mode === 'month' ? addMonths(this.anchorDate(), 1)
        : addDays(this.anchorDate(), 7));
    this.loadEvents();
  }

  goToToday(): void {
    this.anchorDate.set(new Date());
    this.loadEvents();
  }

  goToDay(date: Date): void {
    this.anchorDate.set(date);
    this.viewMode.set('day');
    this.loadEvents();
  }

  dayLabel(day: Date): string {
    return DAY_NAMES[day.getDay()];
  }

  isToday(day: Date): boolean {
    return isSameDay(day, new Date());
  }

  isConnectionHidden(connectionId: number | undefined): boolean {
    return connectionId != null && this.hiddenConnectionIds().has(connectionId);
  }

  toggleConnectionFilter(connectionId: number | undefined): void {
    if (connectionId == null) return;

    this.hiddenConnectionIds.update(hidden => {
      const next = new Set(hidden);
      if (next.has(connectionId)) {
        next.delete(connectionId);
      } else {
        next.add(connectionId);
      }
      return next;
    });
  }

  hourLabel(hour: number): string {
    if (hour === 0) return '12 AM';
    if (hour === 12) return '12 PM';
    return hour < 12 ? `${hour} AM` : `${hour - 12} PM`;
  }

  openEventDialog(event: CalendarEventDto): void {
    this.selectedEvent.set(event);
    this.eventDialogRef.nativeElement.showModal();
  }

  closeEventDialog(): void {
    this.eventDialogRef.nativeElement.close();
    this.selectedEvent.set(null);
  }

  formatEventTime(event: CalendarEventDto): string {
    const start = new Date(event.startUtc!);
    const end = new Date(event.endUtc!);
    const dateLabel = `${DAY_NAMES[start.getDay()]}, ${MONTH_NAMES[start.getMonth()]} ${start.getDate()}, ${start.getFullYear()}`;

    if (event.isAllDay) {
      return `${dateLabel} · All day`;
    }

    return `${dateLabel} · ${TIME_FORMATTER.format(start)} – ${TIME_FORMATTER.format(end)}`;
  }

  attendeeStatusIcon(status: number | undefined): string {
    switch (status) {
      case 1: return 'x';
      case 2: return 'help-circle';
      case 3: return 'check';
      default: return 'circle';
    }
  }

  attendeeStatusLabel(status: number | undefined): string {
    switch (status) {
      case 1: return 'Declined';
      case 2: return 'Tentative';
      case 3: return 'Accepted';
      default: return 'No response';
    }
  }

  /** On day view, scroll the hour grid so the day's first event sits ~20% down the viewport instead of at the very top. */
  private scrollDayViewToFirstEvent(): void {
    if (this.viewMode() !== 'day') return;

    requestAnimationFrame(() => {
      const container = this.gridBodyRef?.nativeElement;
      if (!container) return;

      const day = this.days()[0];
      const timedEvents = this.filteredEvents().filter(e => !e.isAllDay && isSameDay(new Date(e.startUtc!), day));

      const firstEventMinutes = timedEvents.length > 0
        ? Math.min(...timedEvents.map(e => this.minutesSinceMidnight(new Date(e.startUtc!))))
        : DEFAULT_DAY_SCROLL_HOUR * 60;

      const firstEventTopPx = (firstEventMinutes / 60) * HOUR_HEIGHT_PX;
      const targetScroll = firstEventTopPx - container.clientHeight * DAY_SCROLL_TARGET_FRACTION;
      container.scrollTop = Math.max(0, targetScroll);
    });
  }

  private minutesSinceMidnight(date: Date): number {
    return date.getHours() * 60 + date.getMinutes();
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
