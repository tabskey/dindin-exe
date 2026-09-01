import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './context/auth'
import { AuthProvider } from './context/AuthProvider'
import { ExtratoPage } from './pages/ExtratoPage'
import { LoginPage } from './pages/LoginPage'

function AppRoutes() {
  const { isAuthenticated } = useAuth()

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/extrato" replace /> : <LoginPage />}
      />
      <Route
        path="/extrato"
        element={isAuthenticated ? <ExtratoPage /> : <Navigate to="/login" replace />}
      />
      <Route path="*" element={<Navigate to={isAuthenticated ? '/extrato' : '/login'} replace />} />
    </Routes>
  )
}

function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  )
}

export default App
