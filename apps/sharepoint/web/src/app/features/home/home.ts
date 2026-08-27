import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-home',
    imports: [MatButtonModule],
    templateUrl: './home.html',
    styleUrl: './home.scss',
})
export class Home {
    private readonly router = inject(Router);
    protected readonly authService = inject(AuthService);

    async onLogout(): Promise<void> {
        await this.authService.logout();
        await this.router.navigate(['/login']);
    }
}
