import { Route, Routes } from 'react-router';
import { AppShell } from './components/AppShell';
import { CardDetailPage } from './pages/CardDetailPage';
import { CardListPage } from './pages/CardListPage';

/** Routes (DEC-20): `/` card list, `/cards/:id` card detail. Complete in PR-00. */
export function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<CardListPage />} />
        <Route path="/cards/:id" element={<CardDetailPage />} />
      </Routes>
    </AppShell>
  );
}
