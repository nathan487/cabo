cmake_minimum_required(VERSION 3.10)
project(CaboCliClient)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Protobuf — use anaconda path if system lib not available
find_package(Protobuf QUIET)
if(NOT Protobuf_FOUND)
    set(PROTOBUF_INCLUDE_DIR "$ENV{HOME}/anaconda3/include")
    set(PROTOBUF_LIB_DIR "$ENV{HOME}/anaconda3/lib")
    set(PROTOBUF_LIBRARY "$ENV{HOME}/anaconda3/lib/libprotobuf.so")
    message(STATUS "Using anaconda protobuf: ${PROTOBUF_INCLUDE_DIR}")
else()
    set(PROTOBUF_LIB_DIR "${Protobuf_LIBRARY_DIRS}")
    set(PROTOBUF_LIBRARY ${Protobuf_LIBRARIES})
    message(STATUS "Using system protobuf: ${PROTOBUF_INCLUDE_DIR}")
endif()

# 包含目录
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/proto
    ${PROTOBUF_INCLUDE_DIR}
)

# 客户端源文件
set(CLIENT_SOURCES
    src/main.cpp
)

# protobuf生成文件（复用服务端的）
set(PROTO_SOURCES
    ../src/proto/messages.pb.cc
    ../src/proto/room.pb.cc
    ../src/proto/game.pb.cc
    ../src/proto/common.pb.cc
    ../src/proto/sync.pb.cc
)

# 可执行文件
add_executable(cabo_cli_client
    ${CLIENT_SOURCES}
    ${PROTO_SOURCES}
)

# Protobuf 3.20 anaconda build: InternalMetadata destructor is not exported.
# NDEBUG triggers the inline definition in metadata_lite.h.
target_compile_definitions(cabo_cli_client PRIVATE NDEBUG)

# 链接库
target_link_libraries(cabo_cli_client
    ${PROTOBUF_LIBRARY}
    pthread
)
