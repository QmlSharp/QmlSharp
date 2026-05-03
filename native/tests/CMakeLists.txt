add_executable(qmlsharp_native_tests
  native_host_smoke_tests.cpp
  fixtures/RegistrationCounterViewModel.cpp
  fixtures/RegistrationCounterViewModel.h
  fixtures/RegistrationStatusViewModel.cpp
  fixtures/RegistrationStatusViewModel.h
)

target_compile_features(qmlsharp_native_tests PRIVATE cxx_std_20)
target_include_directories(qmlsharp_native_tests
  PRIVATE
    fixtures
    ${CMAKE_CURRENT_SOURCE_DIR}/../src
)
target_link_libraries(qmlsharp_native_tests
  PRIVATE
    qmlsharp_native
    Qt6::Core
    Qt6::Qml
)

set_target_properties(qmlsharp_native_tests
  PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

qmlsharp_apply_warning_settings(qmlsharp_native_tests)
qmlsharp_enable_sanitizers(qmlsharp_native_tests)
qmlsharp_configure_static_analysis(qmlsharp_native_tests)

add_test(NAME qmlsharp_native_tests COMMAND qmlsharp_native_tests)
set_tests_properties(qmlsharp_native_tests PROPERTIES TIMEOUT 30 LABELS "RequiresQt;Native")

if(WIN32 AND DEFINED ENV{QT_DIR} AND NOT "$ENV{QT_DIR}" STREQUAL "")
  set_tests_properties(qmlsharp_native_tests
    PROPERTIES
      ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:$ENV{QT_DIR}/bin"
  )
endif()
