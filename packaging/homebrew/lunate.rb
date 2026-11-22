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
    url "https://github.com/lunate-agent/lunate/releases/download/v0.5.0/lunate-osx-arm64.tar.gz"
    sha256 "21d88b357aab8f67c50fb9e3d64989597ec38972151d1bda073f632077eabe07"
  elsif OS.linux?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.5.0/lunate-linux-x64.tar.gz"
    sha256 "b02efafbb214a5834db151e3e5a51b3af3294568bd06fb3aae7ece576e20f643"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
