class Lunate < Formula
  desc "Native C# coding agent for the terminal"
  homepage "https://github.com/lunate-agent/lunate"
  license "MIT"

  on_macos do
    depends_on arch: :arm64
  end

  on_linux do
    depends_on arch: :x86_64
  end

  if OS.mac?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.6.0/lunate-osx-arm64.tar.gz"
    sha256 "61d44b5e3a6d12867b7ab696d972955fd8f65fae359dc73beab9068a2db46eeb"
  elsif OS.linux?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.6.0/lunate-linux-x64.tar.gz"
    sha256 "d4adfe21bb85e3c4125d81bd4b2002468a306d32d53fcd6a33f8c82a9ddef3cd"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
