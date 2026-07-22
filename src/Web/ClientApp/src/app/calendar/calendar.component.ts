import { Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import {
  CalendarConnectionsClient, CalendarConnectionDto, CalendarEventDto, ColourDto, EventsClient,
  UpdateLocalEventCommand, SetEventColorOverrideCommand
} from '../web-api-client';

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

function pad(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}

function toDateTimeLocal(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toDateOnly(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

interface EventEditorModel {
  id: number;
  title: string;
  isAllDay: boolean;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  colour: string;
}

function toEditorModel(start: Date, end: Date, isAllDay: boolean, title = '', colour = ''): EventEditorModel {
  return {
    id: 0,
    title,
    isAllDay,
    startDate: toDateOnly(start),
    startTime: toDateTimeLocal(start).split('T')[1],
    endDate: toDateOnly(end),
    endTime: toDateTimeLocal(end).split('T')[1],
    colour
  };
}

function editorModelToRange(model: EventEditorModel): { start: Date; end: Date } {
  const start = model.isAllDay ? new Date(`${model.startDate}T00:00`) : new Date(`${model.startDate}T${model.startTime}`);
  const end = model.isAllDay ? new Date(`${model.endDate}T23:59`) : new Date(`${model.endDate}T${model.endTime}`);
  return { start, end };
}

@Component({
  standalone: false,
  selector: 'app-calendar',
  templateUrl: './calendar.component.html',
  styleUrls: ['./calendar.component.scss']
})
export class CalendarComponent implements OnInit {
  @ViewChild('eventDialog') eventDialogRef: ElementRef<HTMLDialogElement>;

  readonly hourHeightPx = HOUR_HEIGHT_PX;
  readonly hours = Array.from({ length: 24 }, (_, i) => i);
  readonly gridHeightPx = 24 * HOUR_HEIGHT_PX;
  readonly dayNames = DAY_NAMES;

  viewMode = signal<ViewMode>('week');
  anchorDate = signal(new Date());
  events = signal<CalendarEventDto[] | null>(null);
  connections = signal<CalendarConnectionDto[]>([]);
  colours = signal<ColourDto[]>([]);
  loading = signal(false);

  searchQuery = signal('');
  hiddenConnectionIds = signal<ReadonlySet<number>>(new Set());

  /** events() narrowed by the in-view search box and any transiently hidden calendars (legend toggles). */
  filteredEvents = computed(() => {
    const events = this.events() ?? [];
    const query = this.searchQuery().trim().toLowerCase();
    const hidden = this.hiddenConnectionIds();

    return events.filter(e =>
      (!query || e.title.toLowerCase().includes(query))
      && !(e.calendarConnectionId != null && hidden.has(e.calendarConnectionId)));
  });

  dialogMode = signal<'edit-local' | 'edit-synced'>('edit-local');
  editingEvent = signal<CalendarEventDto | null>(null);
  eventEditor: EventEditorModel = toEditorModel(new Date(), new Date(), false);
  eventError = signal('');
  saving = signal(false);

  /** The hour-grid days for day/week view. Month view uses monthWeeks() instead. */
  days = computed(() => {
    if (this.viewMode() === 'day') {
      return [startOfDay(this.anchorDate())];
    }
    const start = startOfWeek(this.anchorDate());
    return Array.from({ length: 7 }, (_, i) => addDays(start, i));
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
      next: vm => {
        this.connections.set((vm.connections ?? []).filter(c => c.isVisible));
        this.colours.set(vm.colours ?? []);
      },
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
    this.editingEvent.set(event);
    this.dialogMode.set(event.isLocal ? 'edit-local' : 'edit-synced');
    this.eventEditor = {
      ...toEditorModel(new Date(event.startUtc!), new Date(event.endUtc!), !!event.isAllDay, event.title, event.colour ?? this.colours()[0]?.code ?? ''),
      id: event.id!
    };
    this.eventError.set('');
    this.eventDialogRef.nativeElement.showModal();
  }

  closeEventDialog(): void {
    this.eventDialogRef.nativeElement.close();
    this.editingEvent.set(null);
    this.eventError.set('');
  }

  saveEvent(): void {
    const { start, end } = editorModelToRange(this.eventEditor);

    if (end <= start) {
      this.eventError.set('End time must be after the start time.');
      return;
    }

    this.saving.set(true);

    if (this.dialogMode() === 'edit-synced') {
      const command = new SetEventColorOverrideCommand({ id: this.eventEditor.id, colour: this.eventEditor.colour });
      this.eventsClient.setEventColorOverride(this.eventEditor.id, command).subscribe({
        next: () => this.onSaveSucceeded(),
        error: error => this.onSaveFailed(error)
      });
      return;
    }

    const command = new UpdateLocalEventCommand({
      id: this.eventEditor.id,
      title: this.eventEditor.title,
      startUtc: start,
      endUtc: end,
      isAllDay: this.eventEditor.isAllDay,
      colour: this.eventEditor.colour
    });
    this.eventsClient.updateLocalEvent(this.eventEditor.id, command).subscribe({
      next: () => this.onSaveSucceeded(),
      error: error => this.onSaveFailed(error)
    });
  }

  deleteEvent(): void {
    const id = this.eventEditor.id;
    this.saving.set(true);
    this.eventsClient.deleteLocalEvent(id).subscribe({
      next: () => this.onSaveSucceeded(),
      error: error => this.onSaveFailed(error)
    });
  }

  private onSaveSucceeded(): void {
    this.saving.set(false);
    this.closeEventDialog();
    this.loadEvents();
  }

  private onSaveFailed(error: unknown): void {
    console.error(error);
    this.saving.set(false);
    this.eventError.set('Could not save the event. Please try again.');
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
