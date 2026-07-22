import { APP_ID, NgModule, inject, provideAppInitializer, isDevMode } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  LucideAngularModule, Sun, Moon, Laptop, Plus, Settings, MoreHorizontal,
  RefreshCw, Trash2, ChevronLeft, ChevronRight, CalendarDays, Link2, Search, Menu, X
} from 'lucide-angular';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import { ThemeToggleComponent } from './theme-toggle/theme-toggle.component';
import { CalendarComponent } from './calendar/calendar.component';
import { ConnectionsComponent } from './connections/connections.component';
import { OAuthCallbackComponent } from './connections/oauth-callback/oauth-callback.component';
import { API_BASE_URL } from './web-api-client';
import { AuthorizeInterceptor } from 'src/api-authorization/authorize.interceptor';
import { LoginComponent } from 'src/api-authorization/login/login.component';
import { RegisterComponent } from 'src/api-authorization/register/register.component';
import { ForgotPasswordComponent } from 'src/api-authorization/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from 'src/api-authorization/reset-password/reset-password.component';
import { AuthGuard } from 'src/api-authorization/auth.guard';
import { AuthService } from 'src/api-authorization/auth.service';
import { ServiceWorkerModule } from '@angular/service-worker';

export function getApiBaseUrl(): string {
  const url = document.getElementsByTagName('base')[0].href;
  return url.endsWith('/') ? url.slice(0, -1) : url;
}

@NgModule({
    declarations: [
        AppComponent,
        NavMenuComponent,
        HomeComponent,
        ThemeToggleComponent,
        CalendarComponent,
        ConnectionsComponent,
        OAuthCallbackComponent,
        LoginComponent,
        RegisterComponent,
        ForgotPasswordComponent,
        ResetPasswordComponent
    ],
    bootstrap: [AppComponent],
    imports: [
        BrowserModule,
        FormsModule,
        LucideAngularModule.pick({
            Sun, Moon, Laptop, Plus, Settings, MoreHorizontal,
            RefreshCw, Trash2, ChevronLeft, ChevronRight, CalendarDays, Link2, Search, Menu, X
        }),
        RouterModule.forRoot([
            { path: '', component: CalendarComponent, pathMatch: 'full', canActivate: [AuthGuard] },
            { path: 'connections', component: ConnectionsComponent, canActivate: [AuthGuard] },
            { path: 'connections/callback/:provider', component: OAuthCallbackComponent, canActivate: [AuthGuard] },
            { path: 'login', component: LoginComponent },
            { path: 'register', component: RegisterComponent },
            { path: 'forgot-password', component: ForgotPasswordComponent },
            { path: 'reset-password', component: ResetPasswordComponent }
        ]),

      ServiceWorkerModule.register('ngsw-worker.js', {
        enabled: !isDevMode(),
        // Register the ServiceWorker as soon as the application is stable
        // or after 30 seconds (whichever comes first).
        registrationStrategy: 'registerWhenStable:30000'
      })

    ],
    providers: [
        { provide: APP_ID, useValue: 'ng-cli-universal' },
        { provide: HTTP_INTERCEPTORS, useClass: AuthorizeInterceptor, multi: true },
        { provide: API_BASE_URL, useFactory: getApiBaseUrl, deps: [] },
        provideAppInitializer(() => inject(AuthService).initialize()),
        provideHttpClient(withInterceptorsFromDi())
    ]
})
export class AppModule { }
