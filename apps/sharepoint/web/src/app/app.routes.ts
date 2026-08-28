import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
    {
        path: 'login',
        loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
    },
    {
        path: 'register',
        loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
    },
    {
        path: '',
        loadComponent: () =>
            import('./features/files/file-explorer/file-explorer').then((m) => m.FileExplorer),
        canActivate: [authGuard],
    },
    { path: '**', redirectTo: '' },
];
