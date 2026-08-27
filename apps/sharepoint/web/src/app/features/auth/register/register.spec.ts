import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Register } from './register';
import { AuthService } from '../../../core/auth/auth.service';

describe('Register', () => {
    let authServiceStub: { register: ReturnType<typeof vi.fn> };
    let router: Router;

    beforeEach(async () => {
        authServiceStub = { register: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [Register],
            providers: [provideRouter([]), { provide: AuthService, useValue: authServiceStub }],
        }).compileComponents();

        router = TestBed.inject(Router);
        vi.spyOn(router, 'navigate').mockResolvedValue(true);
    });

    it('form is invalid when empty, and invalid when password is too short', () => {
        const fixture = TestBed.createComponent(Register);
        const component = fixture.componentInstance;

        expect(component.form.invalid).toBe(true);

        component.form.setValue({
            email: 'jane@example.com',
            password: 'short',
            orgName: 'Acme',
        });
        expect(component.form.invalid).toBe(true);

        component.form.controls.password.setValue('password123');
        expect(component.form.valid).toBe(true);
    });

    it('successful submit calls AuthService.register and navigates to /', async () => {
        authServiceStub.register.mockResolvedValue(undefined);
        const fixture = TestBed.createComponent(Register);
        const component = fixture.componentInstance;

        component.form.setValue({
            email: 'jane@example.com',
            password: 'password123',
            orgName: 'Acme',
        });
        await component.onSubmit();

        expect(authServiceStub.register).toHaveBeenCalledWith({
            email: 'jane@example.com',
            password: 'password123',
            orgName: 'Acme',
        });
        expect(router.navigate).toHaveBeenCalledWith(['/']);
        expect(component.errorMessage()).toBeNull();
    });

    it('failed submit renders the error message and does not navigate', async () => {
        authServiceStub.register.mockRejectedValue(
            new HttpErrorResponse({
                status: 409,
                error: { message: 'Email already registered.' },
            }),
        );
        const fixture = TestBed.createComponent(Register);
        const component = fixture.componentInstance;

        component.form.setValue({
            email: 'jane@example.com',
            password: 'password123',
            orgName: 'Acme',
        });
        await component.onSubmit();

        expect(component.errorMessage()).toBe('Email already registered.');
        expect(router.navigate).not.toHaveBeenCalled();
    });
});
