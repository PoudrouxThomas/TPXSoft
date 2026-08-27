import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
    selector: 'app-register',
    imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
    templateUrl: './register.html',
    styleUrl: './register.scss',
})
export class Register {
    private readonly fb = inject(FormBuilder);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);

    readonly errorMessage = signal<string | null>(null);
    readonly submitting = signal(false);

    readonly form = this.fb.nonNullable.group({
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, Validators.minLength(8)]],
        orgName: ['', [Validators.required]],
    });

    async onSubmit(): Promise<void> {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        this.errorMessage.set(null);
        this.submitting.set(true);
        try {
            await this.authService.register(this.form.getRawValue());
            await this.router.navigate(['/']);
        } catch (e) {
            const message =
                e instanceof HttpErrorResponse && e.error?.message
                    ? e.error.message
                    : 'Something went wrong.';
            this.errorMessage.set(message);
        } finally {
            this.submitting.set(false);
        }
    }
}
