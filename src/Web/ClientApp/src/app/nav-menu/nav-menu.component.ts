import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from 'src/api-authorization/auth.service';

@Component({
  standalone: false,
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.scss']
})
export class NavMenuComponent {
  isAuthenticated$ = this.authService.isAuthenticated$;
  menuOpen = signal(false);

  constructor(private authService: AuthService, private router: Router) {}

  toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  logout(event: Event): void {
    event.preventDefault();
    this.closeMenu();
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login'])
    });
  }
}
