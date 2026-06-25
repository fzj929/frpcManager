<template>
  <div class="users-page">
    <div class="page-header">
      <h2>用户管理</h2>
      <el-button type="primary" :icon="Plus" @click="openCreateDialog">新增用户</el-button>
    </div>

    <el-card class="table-card">
      <el-table :data="users" v-loading="loading" row-key="id" stripe>
        <el-table-column prop="username" label="用户名" min-width="160" />
        <el-table-column label="角色" width="130">
          <template #default="{ row }">
            <el-tag :type="row.role === 'admin' ? 'danger' : 'info'">
              {{ roleLabel(row.role) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="110">
          <template #default="{ row }">
            <el-tag :type="row.isDisabled ? 'warning' : 'success'">
              {{ row.isDisabled ? '已禁用' : '正常' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" min-width="180">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button size="small" :icon="Edit" @click="openEditDialog(row)">编辑</el-button>
            <el-button size="small" :icon="Key" @click="openResetDialog(row)">重置密码</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="editVisible" :title="editingUser ? '编辑用户' : '新增用户'" width="420px">
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="90px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model.trim="editForm.username" :disabled="!!editingUser" />
        </el-form-item>
        <el-form-item v-if="!editingUser" label="密码" prop="password">
          <el-input v-model="editForm.password" type="password" show-password />
        </el-form-item>
        <el-form-item label="角色" prop="role">
          <el-select v-model="editForm.role" style="width: 100%">
            <el-option label="管理员" value="admin" />
            <el-option label="普通用户" value="user" />
          </el-select>
        </el-form-item>
        <el-form-item label="禁用">
          <el-switch v-model="editForm.isDisabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveUser">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="resetVisible" title="重置密码" width="420px">
      <el-form ref="resetFormRef" :model="resetForm" :rules="resetRules" label-width="90px">
        <el-form-item label="用户">
          <el-input :model-value="resetTarget?.username" disabled />
        </el-form-item>
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="resetForm.newPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetVisible = false">取消</el-button>
        <el-button type="primary" :loading="resetting" @click="resetPassword">重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Edit, Key, Plus } from '@element-plus/icons-vue'
import { createUser, fetchUsers, resetUserPassword, updateUser } from '@/api'
import type { UserAccount } from '@/types'
import { formatDateTime as formatTime } from '@/utils/date'

const users = ref<UserAccount[]>([])
const loading = ref(false)
const saving = ref(false)
const resetting = ref(false)
const editVisible = ref(false)
const resetVisible = ref(false)
const editingUser = ref<UserAccount | null>(null)
const resetTarget = ref<UserAccount | null>(null)
const editFormRef = ref<FormInstance>()
const resetFormRef = ref<FormInstance>()

const editForm = reactive({
  username: '',
  password: '',
  role: 'user',
  isDisabled: false
})

const resetForm = reactive({
  newPassword: ''
})

const editRules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ min: 8, message: '密码不能少于 8 位', trigger: 'blur' }],
  role: [{ required: true, message: '请选择角色', trigger: 'change' }]
}

const resetRules: FormRules = {
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 8, message: '密码不能少于 8 位', trigger: 'blur' }
  ]
}

async function loadUsers() {
  loading.value = true
  try {
    const res = await fetchUsers()
    users.value = res.data
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '加载用户失败'
    ElMessage.error(msg)
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  editingUser.value = null
  editForm.username = ''
  editForm.password = ''
  editForm.role = 'user'
  editForm.isDisabled = false
  editVisible.value = true
  editFormRef.value?.clearValidate()
}

function openEditDialog(user: UserAccount) {
  editingUser.value = user
  editForm.username = user.username
  editForm.password = ''
  editForm.role = user.role
  editForm.isDisabled = user.isDisabled
  editVisible.value = true
  editFormRef.value?.clearValidate()
}

async function saveUser() {
  if (!await editFormRef.value?.validate().catch(() => false)) return

  saving.value = true
  try {
    if (editingUser.value) {
      await updateUser(editingUser.value.id, {
        role: editForm.role,
        isDisabled: editForm.isDisabled
      })
      ElMessage.success('用户已更新')
    } else {
      await createUser({
        username: editForm.username,
        password: editForm.password,
        role: editForm.role
      })
      ElMessage.success('用户已创建')
    }
    editVisible.value = false
    await loadUsers()
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '保存失败'
    ElMessage.error(msg)
  } finally {
    saving.value = false
  }
}

function openResetDialog(user: UserAccount) {
  resetTarget.value = user
  resetForm.newPassword = ''
  resetVisible.value = true
  resetFormRef.value?.clearValidate()
}

async function resetPassword() {
  if (!resetTarget.value) return
  if (!await resetFormRef.value?.validate().catch(() => false)) return

  resetting.value = true
  try {
    await resetUserPassword(resetTarget.value.id, resetForm.newPassword)
    ElMessage.success('密码已重置')
    resetVisible.value = false
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '重置失败'
    ElMessage.error(msg)
  } finally {
    resetting.value = false
  }
}

function roleLabel(role: string) {
  return role === 'admin' ? '管理员' : '普通用户'
}

onMounted(loadUsers)
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  gap: 12px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
}

.table-card {
  border-radius: 8px;
}

@media (max-width: 768px) {
  .page-header {
    align-items: stretch;
    flex-direction: column;
  }

  .page-header :deep(.el-button) {
    width: 100%;
  }

  .table-card :deep(.el-card__body) {
    overflow-x: auto;
  }

  .table-card :deep(.el-table) {
    min-width: 760px;
  }
}
</style>
