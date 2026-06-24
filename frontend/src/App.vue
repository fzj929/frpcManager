<template>
  <el-config-provider :locale="elementLocale">
    <router-view />
    <div class="language-switcher" data-no-translate>
      <el-button-group>
        <el-button
          size="small"
          :type="language === 'zh' ? 'primary' : 'default'"
          @click="setLanguage('zh')"
        >
          中文
        </el-button>
        <el-button
          size="small"
          :type="language === 'en' ? 'primary' : 'default'"
          @click="setLanguage('en')"
        >
          English
        </el-button>
      </el-button-group>
    </div>
  </el-config-provider>
</template>

<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  applyPageTranslations,
  elementLocale,
  language,
  setLanguage,
} from '@/i18n'

const route = useRoute()

onMounted(() => {
  applyPageTranslations()
})

watch([language, () => route.fullPath], () => applyPageTranslations(), { flush: 'post' })
</script>

<style scoped>
.language-switcher {
  position: fixed;
  right: 16px;
  bottom: 16px;
  z-index: 3000;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 6px 20px rgba(15, 23, 42, 0.16);
}

@media (max-width: 768px) {
  .language-switcher {
    right: 12px;
    bottom: 78px;
  }
}
</style>
